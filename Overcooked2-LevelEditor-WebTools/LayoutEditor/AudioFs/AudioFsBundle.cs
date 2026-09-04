using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LayoutEditor.AudioFs
{
    /// <summary>Unity bundle 内的一个存储条目（CAB 序列化文件或 .resS/.resource 资源）。</summary>
    internal sealed class AudioFsBundleEntry
    {
        public string name;
        public long offset; // 在解压后数据流中的偏移
        public long size;
    }

    /// <summary>UnityFS bundle 读取器（仅 2017.4 版格式：version 6, LZ4/LZ4HC 块）。</summary>
    internal sealed class AudioFsBundle
    {
        public string path;
        public readonly List<AudioFsBundleEntry> entries = new List<AudioFsBundleEntry>();
        private byte[] _data;              // 解压后的完整数据流（懒加载）
        private readonly object _lock = new object();

        private AudioFsBundle(string path) { this.path = path; }

        /// <summary>只解析头 + blocksInfo（不解压数据块），用于列出条目。外层头为大端。</summary>
        public static AudioFsBundle Scan(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                var sig = ReadCString(br);
                if (sig != "UnityFS") throw new Exception("not UnityFS: " + path);
                uint version = ReadU32BE(br);
                ReadCString(br); // player version
                ReadCString(br); // engine version
                br.ReadBytes(8); // file size (BE)
                uint compressedSize = ReadU32BE(br);
                uint uncompressedSize = ReadU32BE(br);
                uint flags = ReadU32BE(br);
                if (version >= 7) Align(fs, 16);

                byte[] blocksInfoBytes;
                if ((flags & 0x80) != 0) // blocksInfo at end
                {
                    fs.Seek(-compressedSize, SeekOrigin.End);
                    blocksInfoBytes = br.ReadBytes((int)compressedSize);
                }
                else
                {
                    blocksInfoBytes = br.ReadBytes((int)compressedSize);
                }
                blocksInfoBytes = DecompressFlagged(blocksInfoBytes, uncompressedSize, flags & 0x3F);

                var bundle = new AudioFsBundle(path);
                using (var ms = new MemoryStream(blocksInfoBytes))
                using (var r = new BinaryReader(ms))
                {
                    r.ReadBytes(16); // uncompressed data hash
                    int blockCount = ReadI32BE(r);
                    for (int i = 0; i < blockCount; i++)
                    {
                        ReadU32BE(r); // uncompressed size
                        ReadU32BE(r); // compressed size
                        r.ReadUInt16(); // flags (BE 下同为 2 字节，值域小，直接 LE 读等价于字节交换——按 BE 读)
                    }
                    int nodeCount = ReadI32BE(r);
                    for (int i = 0; i < nodeCount; i++)
                    {
                        var e = new AudioFsBundleEntry();
                        e.offset = ReadI64BE(r);
                        e.size = ReadI64BE(r);
                        ReadU32BE(r); // flags
                        e.name = ReadCString(r);
                        bundle.entries.Add(e);
                    }
                }
                return bundle;
            }
        }

        /// <summary>解压并缓存整个数据流。</summary>
        public byte[] Data
        {
            get
            {
                lock (_lock)
                {
                    if (_data != null) return _data;
                    using (var fs = File.OpenRead(path))
                    using (var br = new BinaryReader(fs))
                    {
                        var sig = ReadCString(br);
                        if (sig != "UnityFS") throw new Exception("not UnityFS");
                        uint version = ReadU32BE(br);
                        ReadCString(br);
                        ReadCString(br);
                        br.ReadBytes(8);
                        uint compressedSize = ReadU32BE(br);
                        uint uncompressedSize = ReadU32BE(br);
                        uint flags = ReadU32BE(br);
                        if (version >= 7) Align(fs, 16);

                        byte[] blocksInfoBytes;
                        if ((flags & 0x80) != 0)
                        {
                            fs.Seek(-compressedSize, SeekOrigin.End);
                            blocksInfoBytes = br.ReadBytes((int)compressedSize);
                        }
                        else
                        {
                            blocksInfoBytes = br.ReadBytes((int)compressedSize);
                        }
                        blocksInfoBytes = DecompressFlagged(blocksInfoBytes, uncompressedSize, flags & 0x3F);

                        var blocks = new List<byte[]>();
                        using (var ms = new MemoryStream(blocksInfoBytes))
                        using (var r = new BinaryReader(ms))
                        {
                            r.ReadBytes(16);
                            int blockCount = ReadI32BE(r);
                            long total = 0;
                            for (int i = 0; i < blockCount; i++)
                            {
                                uint unc = ReadU32BE(r);
                                uint comp = ReadU32BE(r);
                                uint bflags = ReadU16BE(r);
                                var compBytes = br.ReadBytes((int)comp);
                                var uncBytes = DecompressFlagged(compBytes, unc, bflags & 0x3F);
                                blocks.Add(uncBytes);
                                total += uncBytes.Length;
                            }

                            int nodeCount = ReadI32BE(r);
                            for (int i = 0; i < nodeCount; i++)
                            {
                                ReadI64BE(r); ReadI64BE(r); ReadU32BE(r); ReadCString(r);
                            }

                            var data = new byte[total];
                            int pos = 0;
                            foreach (var b in blocks)
                            {
                                Buffer.BlockCopy(b, 0, data, pos, b.Length);
                                pos += b.Length;
                            }
                            _data = data;
                            return data;
                        }
                    }
                }
            }
        }

        /// <summary>取条目的字节切片。</summary>
        public byte[] ReadEntry(AudioFsBundleEntry entry)
        {
            var data = Data;
            var buf = new byte[entry.size];
            Buffer.BlockCopy(data, (int)entry.offset, buf, 0, (int)entry.size);
            return buf;
        }

        public AudioFsBundleEntry FindEntry(string name)
        {
            foreach (var e in entries)
                if (string.Compare(e.name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return e;
            return null;
        }

        private static byte[] DecompressFlagged(byte[] input, uint uncompressedSize, uint compression)
        {
            if (compression == 0) return input;
            if (compression == 2 || compression == 3)
                return AudioFsLz4.Decompress(input, input.Length, (int)uncompressedSize);
            throw new Exception("unsupported bundle compression: " + compression);
        }

        private static uint ReadU32BE(BinaryReader r)
        {
            var b = r.ReadBytes(4);
            return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
        }

        private static int ReadI32BE(BinaryReader r)
        {
            return (int)ReadU32BE(r);
        }

        private static ushort ReadU16BE(BinaryReader r)
        {
            var b = r.ReadBytes(2);
            return (ushort)((b[0] << 8) | b[1]);
        }

        private static long ReadI64BE(BinaryReader r)
        {
            var b = r.ReadBytes(8);
            long v = 0;
            for (int i = 0; i < 8; i++)
                v = (v << 8) | b[i];
            return v;
        }

        private static string ReadCString(BinaryReader r)
        {
            var bytes = new List<byte>(32);
            int b;
            while ((b = r.ReadByte()) != 0)
                bytes.Add((byte)b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static void Align(Stream s, int alignment)
        {
            long rem = s.Position % alignment;
            if (rem != 0) s.Seek(alignment - rem, SeekOrigin.Current);
        }
    }
}

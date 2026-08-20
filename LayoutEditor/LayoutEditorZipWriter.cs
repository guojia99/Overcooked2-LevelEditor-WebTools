using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>手写 store-only（无压缩）zip 打包器。
/// Unity 2017 的 .NET 3.5 profile 没有 System.IO.Compression.ZipFile，
/// 项目内也没有 SharpZipLib/Ionic 等库；而 AssetBundle 本身已内嵌压缩，
/// store 模式不会显著增大体积，且任何解压工具都能打开。
/// 格式：Local File Header + 数据 + Central Directory + EOCD，文件名 UTF-8。</summary>
public static class LayoutEditorZipWriter
{
    public class ZipEntrySource
    {
        public string FileName;   // zip 内相对路径（用 / 分隔）
        public string SourcePath; // 磁盘绝对路径

        public ZipEntrySource(string fileName, string sourcePath)
        {
            FileName = fileName;
            SourcePath = sourcePath;
        }
    }

    private static uint[] _crcTable;

    private static uint[] CrcTable
    {
        get
        {
            if (_crcTable == null)
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    var c = i;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[i] = c;
                }
                _crcTable = table;
            }
            return _crcTable;
        }
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        var table = CrcTable;
        for (int i = 0; i < data.Length; i++)
            crc = table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>打包 entries 到 zipPath（覆盖已有文件）。zip 内不含目录条目，
    /// 解压工具会按路径自动建目录。</summary>
    public static void WriteZip(string zipPath, List<ZipEntrySource> entries)
    {
        var dir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            var central = new List<byte[]>();
            long offset = 0;

            foreach (var e in entries)
            {
                var data = File.ReadAllBytes(e.SourcePath);
                var nameBytes = Encoding.UTF8.GetBytes(e.FileName.Replace('\\', '/'));
                var crc = Crc32(data);
                var stamp = DosDateTime(DateTime.Now);
                var time = stamp & 0xFFFF;
                var date = (stamp >> 16) & 0xFFFF;

                // Local File Header
                w.Write(0x04034b50u);
                w.Write((ushort)20);          // version needed
                w.Write((ushort)0x0800);      // flags: UTF-8 name
                w.Write((ushort)0);           // method: store
                w.Write((ushort)time);
                w.Write((ushort)date);
                w.Write(crc);
                w.Write((uint)data.Length);   // compressed
                w.Write((uint)data.Length);   // uncompressed
                w.Write((ushort)nameBytes.Length);
                w.Write((ushort)0);           // extra len
                w.Write(nameBytes);
                w.Write(data);

                // Central Directory record
                var cd = new MemoryStream();
                using (var cw = new BinaryWriter(cd))
                {
                    cw.Write(0x02014b50u);
                    cw.Write((ushort)20);     // version made by
                    cw.Write((ushort)20);     // version needed
                    cw.Write((ushort)0x0800); // flags
                    cw.Write((ushort)0);      // method
                    cw.Write((ushort)time);
                    cw.Write((ushort)date);
                    cw.Write(crc);
                    cw.Write((uint)data.Length);
                    cw.Write((uint)data.Length);
                    cw.Write((ushort)nameBytes.Length);
                    cw.Write((ushort)0);      // extra len
                    cw.Write((ushort)0);      // comment len
                    cw.Write((ushort)0);      // disk number start
                    cw.Write((ushort)0);      // internal attrs
                    cw.Write((uint)0);        // external attrs
                    cw.Write((uint)offset);   // local header offset
                    cw.Write(nameBytes);
                }
                central.Add(cd.ToArray());

                offset += 30 + nameBytes.Length + data.Length;
            }

            long cdOffset = offset;
            long cdSize = 0;
            foreach (var b in central)
            {
                w.Write(b);
                cdSize += b.Length;
            }

            // End of Central Directory
            w.Write(0x06054b50u);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)entries.Count);
            w.Write((ushort)entries.Count);
            w.Write((uint)cdSize);
            w.Write((uint)cdOffset);
            w.Write((ushort)0);
        }
    }

    /// <summary>DOS 时间打包：(date &lt;&lt; 16) | time。</summary>
    private static uint DosDateTime(DateTime t)
    {
        var year = Math.Max(1980, Math.Min(2107, t.Year));
        var date = (uint)((year - 1980) << 9 | t.Month << 5 | t.Day);
        var time = (uint)(t.Hour << 11 | t.Minute << 5 | t.Second / 2);
        return (date << 16) | time;
    }
}

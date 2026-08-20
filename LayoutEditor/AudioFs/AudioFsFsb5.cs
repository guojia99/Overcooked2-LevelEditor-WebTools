using System;
using System.Collections.Generic;
using System.IO;

namespace LayoutEditor.AudioFs
{
    /// <summary>FSB5 采样信息。</summary>
    internal sealed class AudioFsSample
    {
        public string name;
        public int frequency;
        public int channels;
        public long dataOffset;
        public long samples;      // 总采样数（每声道）
        public long vorbisCrc32 = -1;
        public byte[] data;       // 压缩数据
    }

    /// <summary>FSB5 容器解析 + Vorbis 重组 ogg + IMA ADPCM 解码 + PCM WAV 写出。</summary>
    internal static class AudioFsFsb5
    {
        public const int ModePcm8 = 1;
        public const int ModePcm16 = 2;
        public const int ModeImaAdpcm = 7;
        public const int ModeVorbis = 15;

        public static int Mode(byte[] fsb)
        {
            // id(4) version(4) numSamples(4) sampleHeadersSize(4) nameTableSize(4) dataSize(4) mode(4)
            return BitConverter.ToInt32(fsb, 24);
        }

        public static List<AudioFsSample> Parse(byte[] fsb)
        {
            if (fsb.Length < 60 || fsb[0] != (byte)'F' || fsb[1] != (byte)'S' || fsb[2] != (byte)'B' || fsb[3] != (byte)'5')
                throw new Exception("not FSB5");
            int version = ReadI32(fsb, 4);
            int numSamples = ReadI32(fsb, 8);
            int sampleHeadersSize = ReadI32(fsb, 12);
            int nameTableSize = ReadI32(fsb, 16);
            int mode = ReadI32(fsb, 20);
            int headerSize = 60;
            if (version == 0) headerSize += 4; // 旧版多一个 unknown 字段（60 固定头 + 4）
            // 注：fsb5 固定头为 4+4*6 + 8 + 16 + 8 = 60；version==0 时再读 4 字节 unknown

            var samples = new List<AudioFsSample>(numSamples);
            int pos = headerSize;
            int[] frequencyValues = { 0, 8000, 11000, 11025, 16000, 22050, 24000, 32000, 44100, 48000 };

            for (int i = 0; i < numSamples; i++)
            {
                ulong raw = ReadU64(fsb, pos); pos += 8;
                bool nextChunk = (raw & 1UL) != 0;
                int freqIdx = (int)((raw >> 1) & 0xF);
                int channels = (int)((raw >> 5) & 1) + 1;
                long dataOffset = (long)((raw >> 6) & 0xFFFFFFFUL) * 16;
                long totalSamples = (long)((raw >> 34) & 0x3FFFFFFFUL);

                var s = new AudioFsSample();
                s.channels = channels;
                s.dataOffset = dataOffset;
                s.samples = totalSamples;
                s.frequency = freqIdx >= 0 && freqIdx < frequencyValues.Length ? frequencyValues[freqIdx] : 0;

                while (nextChunk)
                {
                    uint c = ReadU32(fsb, pos); pos += 4;
                    nextChunk = (c & 1) != 0;
                    int chunkSize = (int)((c >> 1) & 0xFFFFFF);
                    int chunkType = (int)((c >> 25) & 0x7F);
                    if (chunkType == 11) // VORBISDATA
                    {
                        s.vorbisCrc32 = ReadU32(fsb, pos) & 0xFFFFFFFFL;
                        pos += chunkSize;
                    }
                    else if (chunkType == 2) // FREQUENCY
                    {
                        s.frequency = ReadI32(fsb, pos);
                        pos += chunkSize;
                    }
                    else
                    {
                        pos += chunkSize;
                    }
                }
                if (s.frequency == 0) throw new Exception("unknown frequency");
                samples.Add(s);
            }

            // 名字表
            int nameTableStart = pos;
            if (nameTableSize > 0)
            {
                int[] nameOffsets = new int[numSamples];
                for (int i = 0; i < numSamples; i++)
                {
                    nameOffsets[i] = ReadI32(fsb, pos);
                    pos += 4;
                }
                for (int i = 0; i < numSamples; i++)
                {
                    int p = nameTableStart + nameOffsets[i];
                    int end = p;
                    while (fsb[end] != 0) end++;
                    var chars = new char[end - p];
                    for (int j = 0; j < end - p; j++) chars[j] = (char)fsb[p + j];
                    samples[i].name = new string(chars);
                }
            }
            for (int i = 0; i < samples.Count; i++)
                if (samples[i].name == null) samples[i].name = i.ToString("0000");

            // 数据
            int dataStart = headerSize + sampleHeadersSize + nameTableSize;
            for (int i = 0; i < samples.Count; i++)
            {
                long start = dataStart + samples[i].dataOffset;
                long endPos = i < samples.Count - 1 ? dataStart + samples[i + 1].dataOffset : fsb.Length;
                var buf = new byte[endPos - start];
                Buffer.BlockCopy(fsb, (int)start, buf, 0, buf.Length);
                samples[i].data = buf;
            }
            return samples;
        }

        // ---------------- Vorbis -> ogg 重组（无需解码） ----------------

        /// <summary>用预生成的 setup header 重建 ogg。granulepos 仅在最后一页给出（规范允许）。</summary>
        public static byte[] RebuildVorbisOgg(AudioFsSample s, AudioFsVorbisSetup setup, int serialNo)
        {
            var idHeader = BuildIdHeader(s.channels, s.frequency, setup.blocksizeShort, setup.blocksizeLong);
            var commentHeader = BuildCommentHeader();
            var setupHeader = setup.setupHeader;

            var muxer = new AudioFsOggMuxer(serialNo);
            muxer.WriteHeaderPackets(idHeader, commentHeader, setupHeader);

            // 音频包：u16 长度前缀
            var data = s.data;
            int pos = 0;
            var packets = new List<byte[]>();
            while (pos + 2 <= data.Length)
            {
                int len = data[pos] | (data[pos + 1] << 8);
                pos += 2;
                if (pos + len > data.Length) throw new Exception("vorbis packet overrun");
                var pkt = new byte[len];
                Buffer.BlockCopy(data, pos, pkt, 0, len);
                packets.Add(pkt);
                pos += len;
            }
            if (packets.Count == 0) throw new Exception("no vorbis packets");
            muxer.TotalSamples = s.samples; // 必须在写包前设置（最后一页的 granulepos）
            for (int i = 0; i < packets.Count; i++)
                muxer.WriteAudioPacket(packets[i], i == packets.Count - 1);
            muxer.Finish();
            return muxer.ToArray();
        }

        private static byte[] BuildIdHeader(int channels, int rate, int bsShort, int bsLong)
        {
            int bs0 = Log2(bsShort); // ilog(bs)-1 = log2
            int bs1 = Log2(bsLong);
            // 位写顺序：1(8) "vorbis"(48) version(32=0) ch(8) rate(32) bitrate_max(32=0)
            // bitrate_nominal(32=0) bitrate_min(32=0) bs0(4) bs1(4) framing(1)
            var bits = new AudioFsBitWriter(32);
            bits.Write(0x01, 8);
            bits.Write('v', 8); bits.Write('o', 8); bits.Write('r', 8); bits.Write('b', 8); bits.Write('i', 8); bits.Write('s', 8);
            bits.Write(0, 32);
            bits.Write(channels, 8);
            bits.Write((uint)rate, 32);
            bits.Write(0, 32);
            bits.Write(0, 32);
            bits.Write(0, 32);
            bits.Write(bs0, 4);
            bits.Write(bs1, 4);
            bits.Write(1, 1);
            return bits.ToArray();
        }

        private static byte[] BuildCommentHeader()
        {
            // \x03vorbis + vendor + 无注释 + framing bit
            var bits = new AudioFsBitWriter(64);
            bits.Write(0x03, 8);
            bits.Write('v', 8); bits.Write('o', 8); bits.Write('r', 8); bits.Write('b', 8); bits.Write('i', 8); bits.Write('s', 8);
            var vendor = System.Text.Encoding.ASCII.GetBytes("Overcooked2LevelEditor");
            bits.Write((uint)vendor.Length, 32);
            foreach (var b in vendor) bits.Write(b, 8);
            bits.Write(0, 32); // 无用户注释
            bits.Write(1, 1);  // framing
            return bits.ToArray();
        }

        // ---------------- IMA ADPCM（FMOD 块交错） ----------------

        private static readonly int[] ImaStep = {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130,
            143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449,
            494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
            1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026,
            4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442,
            11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
            27086, 29794, 32767 };
        private static readonly int[] ImaIndex = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        /// <summary>FMOD IMA：每块每声道 36 字节（4B 头 + 32B 数据 = 64 样本），块按声道交错。</summary>
        public static short[] DecodeImaAdpcm(AudioFsSample s)
        {
            int ch = s.channels;
            var perCh = new List<short>[ch];
            for (int i = 0; i < ch; i++) perCh[i] = new List<short>(4096);
            byte[] d = s.data;
            int pos = 0;
            while (pos + 36 <= d.Length)
            {
                for (int c = 0; c < ch; c++)
                {
                    int pred = BitConverter.ToInt16(d, pos);
                    int stepIdx = d[pos + 2];
                    if (stepIdx > 88) stepIdx = 0;
                    pos += 4;
                    for (int i = 0; i < 64; i++)
                    {
                        byte b = d[pos + (i >> 1)];
                        int nib = (b >> ((i & 1) * 4)) & 0xF;
                        int step = ImaStep[stepIdx];
                        int diff = step >> 3;
                        if ((nib & 4) != 0) diff += step;
                        if ((nib & 2) != 0) diff += step >> 1;
                        if ((nib & 1) != 0) diff += step >> 2;
                        pred = (nib & 8) != 0 ? pred - diff : pred + diff;
                        if (pred > 32767) pred = 32767;
                        else if (pred < -32768) pred = -32768;
                        int idx = stepIdx + ImaIndex[nib & 7];
                        if (idx < 0) idx = 0; else if (idx > 88) idx = 88;
                        stepIdx = idx;
                        perCh[c].Add((short)pred);
                    }
                    pos += 32;
                }
            }

            int frames = int.MaxValue;
            for (int c = 0; c < ch; c++) frames = Math.Min(frames, perCh[c].Count);
            frames = (int)Math.Min(frames, s.samples);
            var outBuf = new short[frames * ch];
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < ch; c++)
                    outBuf[f * ch + c] = perCh[c][f];
            return outBuf;
        }

        public static byte[] BuildWav(short[] pcm, int channels, int rate)
        {
            int dataBytes = pcm.Length * 2;
            var ms = new MemoryStream(44 + dataBytes);
            var bw = new BinaryWriter(ms);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataBytes);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(rate);
            bw.Write(rate * channels * 2);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);
            var buf = new byte[dataBytes];
            Buffer.BlockCopy(pcm, 0, buf, 0, dataBytes);
            bw.Write(buf);
            return ms.ToArray();
        }

        // ---------------- Ogg 封装 ----------------

        private sealed class AudioFsOggMuxer
        {
            private readonly MemoryStream _ms = new MemoryStream();
            private readonly uint _serial;
            private int _pageSeq;
            private long _granule = -1;
            private readonly List<byte> _body = new List<byte>(8192);
            private readonly List<int> _segments = new List<int>(); // 每段 ≤255；255 表示包未结束
            private int _bodyTaken; // 已随页写出的 body 字节数

            public long TotalSamples { set { _granule = value; } }

            public AudioFsOggMuxer(int serialNo) { _serial = (uint)serialNo; }

            public void WriteHeaderPackets(byte[] id, byte[] comment, byte[] setup)
            {
                AddPacket(id);
                AddPacket(comment);
                AddPacket(setup);
                EmitPage(false); // BOS 页（含三个头包）
            }

            public void WriteAudioPacket(byte[] packet, bool isLast)
            {
                AddPacket(packet);
                if (!isLast && _body.Count - _bodyTaken >= 4096)
                    EmitPage(false);
            }

            public void Finish()
            {
                // 余下的全部写出；仅最后一页 eos + granule（支持大包跨页 continuation）
                while (_segments.Count > 0)
                {
                    bool last = _segments.Count <= 255;
                    EmitPage(last);
                }
            }

            private void AddPacket(byte[] packet)
            {
                _body.AddRange(packet);
                int rem = packet.Length;
                while (rem >= 255)
                {
                    _segments.Add(255);
                    rem -= 255;
                }
                _segments.Add(rem);
            }

            private void EmitPage(bool eos)
            {
                if (_segments.Count == 0) return;
                int segCount = Math.Min(255, _segments.Count);
                int bodyLen = 0;
                for (int i = 0; i < segCount; i++) bodyLen += _segments[i];

                int headerLen = 27 + segCount;
                var page = new byte[headerLen + bodyLen];
                int p = 0;
                page[p++] = (byte)'O'; page[p++] = (byte)'g'; page[p++] = (byte)'g'; page[p++] = (byte)'S';
                page[p++] = 0; // version
                int headerType = _pageSeq == 0 ? 0x02 : 0; // 首页 BOS
                if (eos) headerType |= 0x04;
                page[p++] = (byte)headerType;
                long g = eos ? _granule : -1;
                for (int i = 0; i < 8; i++) page[p + i] = (byte)((ulong)g >> (8 * i));
                p += 8;
                for (int i = 0; i < 4; i++) page[p + i] = (byte)(_serial >> (8 * i));
                p += 4;
                for (int i = 0; i < 4; i++) page[p + i] = (byte)(_pageSeq >> (8 * i));
                p += 4;
                p += 4; // CRC 占位
                page[p++] = (byte)segCount;
                for (int i = 0; i < segCount; i++) page[p + i] = (byte)_segments[i];
                p += segCount;
                _body.CopyTo(_bodyTaken, page, p, bodyLen);
                _bodyTaken += bodyLen;
                uint crc = OggCrc32(page);
                for (int i = 0; i < 4; i++) page[22 + i] = (byte)(crc >> (8 * i));
                _ms.Write(page, 0, page.Length);
                _pageSeq++;

                _segments.RemoveRange(0, segCount);
            }

            public byte[] ToArray() { return _ms.ToArray(); }
        }

        private static uint[] _oggCrcTable;
        private static uint OggCrc32(byte[] data)
        {
            if (_oggCrcTable == null)
            {
                _oggCrcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint r = i << 24;
                    for (int k = 0; k < 8; k++)
                        r = ((r & 0x80000000U) != 0) ? ((r << 1) ^ 0x04C11DB7U) : (r << 1);
                    _oggCrcTable[i] = r;
                }
            }
            uint crc = 0;
            for (int i = 0; i < data.Length; i++)
                crc = (crc << 8) ^ _oggCrcTable[((crc >> 24) & 0xFF) ^ data[i]];
            return crc;
        }

        // ---------------- bit writer ----------------

        private sealed class AudioFsBitWriter
        {
            private readonly List<byte> _bytes = new List<byte>(64);
            private int _bitPos;

            public AudioFsBitWriter(int capacityBits) { }

            public void Write(uint value, int bits)
            {
                for (int i = 0; i < bits; i++)
                {
                    int bit = (int)((value >> i) & 1);
                    if (_bitPos % 8 == 0) _bytes.Add(0);
                    if (bit != 0)
                    {
                        int idx = _bytes.Count - 1;
                        _bytes[idx] = (byte)(_bytes[idx] | (1 << (_bitPos % 8)));
                    }
                    _bitPos++;
                }
            }

            public void Write(int value, int bits) { Write((uint)value, bits); }
            public void Write(char c, int bits) { Write((uint)c, bits); }

            public byte[] ToArray() { return _bytes.ToArray(); }
        }

        private static int Log2(int v)
        {
            int r = 0;
            while ((v >>= 1) != 0) r++;
            return r;
        }

        internal static int ReadI32(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        internal static uint ReadU32(byte[] b, int o) { return BitConverter.ToUInt32(b, o); }
        internal static ulong ReadU64(byte[] b, int o) { return BitConverter.ToUInt64(b, o); }

        private static void WriteI64(byte[] b, ref int p, long v)
        {
            for (int i = 0; i < 8; i++) { b[p + i] = (byte)((ulong)v >> (8 * i)); }
            p += 8;
        }
        private static void WriteU32(byte[] b, ref int p, uint v)
        {
            for (int i = 0; i < 4; i++) { b[p + i] = (byte)(v >> (8 * i)); }
            p += 4;
        }
        private static void WriteI32(byte[] b, ref int p, int v) { WriteU32(b, ref p, (uint)v); }
        private static void WriteU32At(byte[] b, int o, uint v)
        {
            for (int i = 0; i < 4; i++) b[o + i] = (byte)(v >> (8 * i));
        }
    }

    /// <summary>Vorbis 预生成配置。</summary>
    internal sealed class AudioFsVorbisSetup
    {
        public int blocksizeShort;
        public int blocksizeLong;
        public byte[] setupHeader;
    }
}

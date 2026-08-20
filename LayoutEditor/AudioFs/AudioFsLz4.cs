using System;

namespace LayoutEditor.AudioFs
{
    /// <summary>LZ4 块解压（decode only；LZ4/LZ4HC 块格式相同）。</summary>
    internal static class AudioFsLz4
    {
        public static byte[] Decompress(byte[] src, int srcLen, int dstLen)
        {
            var dst = new byte[dstLen];
            int s = 0, d = 0;
            while (true)
            {
                if (s + 1 > srcLen) throw new Exception("LZ4: truncated token");
                int token = src[s++] & 0xFF;

                int litLen = token >> 4;
                if (litLen == 15)
                {
                    int b;
                    do
                    {
                        if (s + 1 > srcLen) throw new Exception("LZ4: truncated literal length");
                        b = src[s++] & 0xFF;
                        litLen += b;
                    } while (b == 255);
                }

                if (s + litLen > srcLen || d + litLen > dstLen) throw new Exception("LZ4: literal overrun");
                Buffer.BlockCopy(src, s, dst, d, litLen);
                s += litLen;
                d += litLen;

                if (s + 2 > srcLen) break; // 末尾无 match
                int offset = (src[s] & 0xFF) | ((src[s + 1] & 0xFF) << 8);
                s += 2;
                if (offset == 0) throw new Exception("LZ4: zero offset");

                int matchLen = (token & 0xF);
                if (matchLen == 15)
                {
                    int b;
                    do
                    {
                        if (s + 1 > srcLen) throw new Exception("LZ4: truncated match length");
                        b = src[s++] & 0xFF;
                        matchLen += b;
                    } while (b == 255);
                }
                matchLen += 4;

                if (d + matchLen > dstLen) throw new Exception("LZ4: match overrun");
                int m = d - offset;
                if (m < 0) throw new Exception("LZ4: bad offset");
                // 重叠拷贝必须逐字节
                for (int i = 0; i < matchLen; i++)
                    dst[d++] = dst[m++];
            }

            if (d != dstLen) throw new Exception("LZ4: size mismatch d=" + d + " expected=" + dstLen);
            return dst;
        }
    }
}

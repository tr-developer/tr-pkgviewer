using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TRPKGViewer.Core
{
    public static class DdsDecoder
    {
        public static Bitmap Decode(byte[] data)
        {
            if (data == null || data.Length < 128 || data[0] != 'D' || data[1] != 'D' || data[2] != 'S' || data[3] != ' ')
                throw new InvalidDataException("Not a DDS file.");

            int height = ReadInt(data, 12);
            int width = ReadInt(data, 16);
            if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                throw new InvalidDataException("Invalid DDS dimensions.");

            uint pfFlags = ReadUInt(data, 80);
            uint fourCC = ReadUInt(data, 84);
            int rgbBits = ReadInt(data, 88);
            uint rMask = ReadUInt(data, 92), gMask = ReadUInt(data, 96), bMask = ReadUInt(data, 100), aMask = ReadUInt(data, 104);

            int offset = 128;
            uint dx10 = FourCC("DX10");
            if (fourCC == dx10)
                throw new NotSupportedException("DDS DX10 textures are not supported yet.");

            int[] pixels;
            if (fourCC == FourCC("DXT1")) pixels = DecodeDxt1(data, offset, width, height);
            else if (fourCC == FourCC("DXT3")) pixels = DecodeDxt3(data, offset, width, height);
            else if (fourCC == FourCC("DXT5")) pixels = DecodeDxt5(data, offset, width, height);
            else if ((pfFlags & 0x40) != 0 && (rgbBits == 32 || rgbBits == 24))
                pixels = DecodeRgb(data, offset, width, height, rgbBits, rMask, gMask, bMask, aMask);
            else
                throw new NotSupportedException("Unsupported DDS format: FourCC=0x" + fourCC.ToString("X8") + ", RGBBits=" + rgbBits);

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var bits = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length); }
            finally { bmp.UnlockBits(bits); }
            return bmp;
        }

        static int[] DecodeDxt1(byte[] d, int o, int w, int h) { return DecodeBlocks(d, o, w, h, 8, 1); }
        static int[] DecodeDxt3(byte[] d, int o, int w, int h) { return DecodeBlocks(d, o, w, h, 16, 3); }
        static int[] DecodeDxt5(byte[] d, int o, int w, int h) { return DecodeBlocks(d, o, w, h, 16, 5); }

        static int[] DecodeBlocks(byte[] d, int o, int w, int h, int blockSize, int mode)
        {
            int[] dst = new int[w * h];
            for (int by = 0; by < h; by += 4)
            for (int bx = 0; bx < w; bx += 4)
            {
                if (o + blockSize > d.Length) throw new EndOfStreamException("Truncated DDS block data.");
                ulong alpha = 0;
                byte a0 = 255, a1 = 255;
                int colorOff = o;
                if (mode == 3) { alpha = ReadULong(d, o); colorOff += 8; }
                else if (mode == 5) { a0 = d[o]; a1 = d[o + 1]; alpha = Read48(d, o + 2); colorOff += 8; }

                ushort c0 = ReadUShort(d, colorOff), c1 = ReadUShort(d, colorOff + 2);
                uint codes = ReadUInt(d, colorOff + 4);
                int[] pal = MakePalette(c0, c1, mode != 1 || c0 > c1);
                byte[] ap = mode == 5 ? MakeAlphaPalette(a0, a1) : null;

                for (int py = 0; py < 4; py++)
                for (int px = 0; px < 4; px++)
                {
                    int n = py * 4 + px, x = bx + px, y = by + py;
                    if (x >= w || y >= h) continue;
                    int argb = pal[(int)((codes >> (2 * n)) & 3)];
                    int a = (argb >> 24) & 255;
                    if (mode == 3) a = (int)((alpha >> (4 * n)) & 15) * 17;
                    else if (mode == 5) a = ap[(int)((alpha >> (3 * n)) & 7)];
                    dst[y * w + x] = (a << 24) | (argb & 0xFFFFFF);
                }
                o += blockSize;
            }
            return dst;
        }

        static int[] MakePalette(ushort c0, ushort c1, bool four)
        {
            int[] p = new int[4];
            p[0] = Rgb565(c0); p[1] = Rgb565(c1);
            if (four)
            {
                p[2] = Mix(p[0], p[1], 2, 1, 3);
                p[3] = Mix(p[0], p[1], 1, 2, 3);
            }
            else
            {
                p[2] = Mix(p[0], p[1], 1, 1, 2);
                p[3] = 0;
            }
            return p;
        }

        static byte[] MakeAlphaPalette(byte a0, byte a1)
        {
            byte[] a = new byte[8]; a[0] = a0; a[1] = a1;
            if (a0 > a1)
                for (int i = 1; i <= 6; i++) a[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            else
            {
                for (int i = 1; i <= 4; i++) a[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                a[6] = 0; a[7] = 255;
            }
            return a;
        }

        static int DecodeChannel(uint v, uint mask)
        {
            if (mask == 0) return 255;
            int shift = 0; while (((mask >> shift) & 1) == 0) shift++;
            uint m = mask >> shift; uint raw = (v & mask) >> shift;
            return (int)(raw * 255 / m);
        }

        static int[] DecodeRgb(byte[] d, int o, int w, int h, int bits, uint rm, uint gm, uint bm, uint am)
        {
            int bpp = bits / 8, pitch = w * bpp;
            int[] dst = new int[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int q = o + y * pitch + x * bpp;
                if (q + bpp > d.Length) throw new EndOfStreamException("Truncated DDS RGB data.");
                uint v = bpp == 4 ? ReadUInt(d, q) : (uint)(d[q] | (d[q + 1] << 8) | (d[q + 2] << 16));
                int r = DecodeChannel(v, rm), g = DecodeChannel(v, gm), b = DecodeChannel(v, bm);
                int a = am == 0 ? 255 : DecodeChannel(v, am);
                dst[y * w + x] = (a << 24) | (r << 16) | (g << 8) | b;
            }
            return dst;
        }

        static int Rgb565(ushort c)
        {
            int r = ((c >> 11) & 31) * 255 / 31, g = ((c >> 5) & 63) * 255 / 63, b = (c & 31) * 255 / 31;
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }
        static int Mix(int a, int b, int wa, int wb, int div)
        {
            int r = (((a >> 16) & 255) * wa + ((b >> 16) & 255) * wb) / div;
            int g = (((a >> 8) & 255) * wa + ((b >> 8) & 255) * wb) / div;
            int bl = ((a & 255) * wa + (b & 255) * wb) / div;
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | bl;
        }
        static uint FourCC(string s) { return (uint)(s[0] | (s[1] << 8) | (s[2] << 16) | (s[3] << 24)); }
        static ushort ReadUShort(byte[] b, int o) { return (ushort)(b[o] | (b[o + 1] << 8)); }
        static uint ReadUInt(byte[] b, int o) { return BitConverter.ToUInt32(b, o); }
        static int ReadInt(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static ulong ReadULong(byte[] b, int o) { return BitConverter.ToUInt64(b, o); }
        static ulong Read48(byte[] b, int o) { ulong v = 0; for (int i = 0; i < 6; i++) v |= (ulong)b[o + i] << (8 * i); return v; }
    }
}

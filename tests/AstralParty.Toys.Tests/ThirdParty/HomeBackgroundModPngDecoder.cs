// 来自 TenkinMori/astralparty-homebg-cesium（src/HomeBackgroundMod/PngDecoder.cs，v1.1.0）。
// MIT License · Copyright (c) 2026 TenkinMori · https://github.com/TenkinMori/astralparty-homebg-cesium
// 原样复制，只改了命名空间和类名：测试里用模组自己的解码器，确认工具箱转出来的 PNG 模组真的读得了。
#nullable disable
using System;
using System.IO;
using System.IO.Compression;

namespace AstralParty.Toys.Tests.ThirdParty
{
    /// <summary>
    /// 最小 PNG 解码器（8-bit、非隔行；支持 RGB / RGBA / 灰度 / 灰度+Alpha / 调色板）。
    /// 用途：Unity 的 ImageConversion.LoadImage 在 HybridCLR 下不一定能反射到，
    /// 这里直接解成 RGBA32 字节，再交给 Texture2D.LoadRawTextureData。
    /// </summary>
    internal static class HomeBackgroundModPngDecoder
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool TryDecode(byte[] data, out int width, out int height, out byte[] rgba)
        {
            width = 0;
            height = 0;
            rgba = null;

            try
            {
                if (data == null || data.Length < 8) return false;
                for (int i = 0; i < 8; i++)
                {
                    if (data[i] != Signature[i]) return false;
                }

                int pos = 8;
                int w = 0, h = 0, bitDepth = 0, colorType = -1, interlace = 0;
                byte[] palette = null;
                MemoryStream idat = new MemoryStream();

                while (pos + 8 <= data.Length)
                {
                    int length = ReadInt32Be(data, pos);
                    pos += 4;
                    if (length < 0 || pos + 4 > data.Length) return false;

                    string type = System.Text.Encoding.ASCII.GetString(data, pos, 4);
                    pos += 4;
                    if (pos + length + 4 > data.Length) return false;

                    int start = pos;
                    if (type == "IHDR")
                    {
                        if (length < 13) return false;
                        w = ReadInt32Be(data, start);
                        h = ReadInt32Be(data, start + 4);
                        bitDepth = data[start + 8];
                        colorType = data[start + 9];
                        int compression = data[start + 10];
                        int filter = data[start + 11];
                        interlace = data[start + 12];
                        if (compression != 0 || filter != 0) return false;
                    }
                    else if (type == "PLTE")
                    {
                        palette = new byte[length];
                        Array.Copy(data, start, palette, 0, length);
                    }
                    else if (type == "IDAT")
                    {
                        idat.Write(data, start, length);
                    }
                    else if (type == "IEND")
                    {
                        break;
                    }

                    pos = start + length + 4;
                }

                if (w <= 0 || h <= 0 || bitDepth != 8 || interlace != 0 || colorType < 0) return false;

                int bpp;
                switch (colorType)
                {
                    case 6: bpp = 4; break;
                    case 2: bpp = 3; break;
                    case 4: bpp = 2; break;
                    case 0: bpp = 1; break;
                    case 3: bpp = 1; break;
                    default: return false;
                }
                if (colorType == 3 && palette == null) return false;

                byte[] raw = Inflate(idat.ToArray());
                if (raw == null) return false;

                int stride = w * bpp;
                if (stride <= 0 || raw.Length < (long)(stride + 1) * h) return false;

                byte[] pixels = new byte[stride * h];
                int prevRow = -1;

                for (int y = 0; y < h; y++)
                {
                    int rowStart = y * (stride + 1);
                    int filter = raw[rowStart];
                    int src = rowStart + 1;
                    int dst = y * stride;

                    for (int x = 0; x < stride; x++)
                    {
                        int a = x >= bpp ? pixels[dst + x - bpp] : 0;
                        int b = prevRow >= 0 ? pixels[prevRow + x] : 0;
                        int c = (prevRow >= 0 && x >= bpp) ? pixels[prevRow + x - bpp] : 0;
                        int value = raw[src + x];
                        int add;
                        switch (filter)
                        {
                            case 0: add = 0; break;
                            case 1: add = a; break;
                            case 2: add = b; break;
                            case 3: add = (a + b) >> 1; break;
                            case 4: add = Paeth(a, b, c); break;
                            default: return false;
                        }
                        pixels[dst + x] = (byte)(value + add);
                    }
                    prevRow = dst;
                }

                rgba = new byte[w * h * 4];
                for (int i = 0, j = 0; i < w * h; i++, j += bpp)
                {
                    byte r, g, b, a;
                    switch (colorType)
                    {
                        case 6:
                            r = pixels[j]; g = pixels[j + 1]; b = pixels[j + 2]; a = pixels[j + 3];
                            break;
                        case 2:
                            r = pixels[j]; g = pixels[j + 1]; b = pixels[j + 2]; a = 255;
                            break;
                        case 4:
                            r = g = b = pixels[j]; a = pixels[j + 1];
                            break;
                        case 0:
                            r = g = b = pixels[j]; a = 255;
                            break;
                        default: // 调色板
                            int index = pixels[j];
                            if (index * 3 + 2 >= palette.Length) return false;
                            r = palette[index * 3]; g = palette[index * 3 + 1]; b = palette[index * 3 + 2]; a = 255;
                            break;
                    }

                    int o = i * 4;
                    rgba[o] = r;
                    rgba[o + 1] = g;
                    rgba[o + 2] = b;
                    rgba[o + 3] = a;
                }

                width = w;
                height = h;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Inflate(byte[] zlib)
        {
            if (zlib == null || zlib.Length < 6) return null;
            if ((zlib[0] & 0x0F) != 8) return null; // CM = deflate

            using (MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6))
            using (DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }

        private static int ReadInt32Be(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            if (pb <= pc) return b;
            return c;
        }
    }
}

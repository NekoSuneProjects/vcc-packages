using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Minimal QR Code encoder (byte mode, error correction level M, versions 1–10, up to 213 bytes),
    /// following the ISO/IEC 18004 algorithm. Used to turn shop and social links into scannable
    /// textures, because Udon can't open a web browser.
    /// </summary>
    internal static class QrCode
    {
        // Level M tables for versions 1..10.
        private static readonly int[] EccPerBlock = { 0, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
        private static readonly int[] NumBlocks = { 0, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };
        private const int FormatBitsM = 0;

        public static bool[,] Encode(string text)
        {
            var data = Encoding.UTF8.GetBytes(text);
            int version = 1;
            for (; version <= 10; version++)
                if (4 + (version < 10 ? 8 : 16) + data.Length * 8 <= DataCodewords(version) * 8) break;
            if (version > 10) throw new ArgumentException("Text too long for a QR code (max about 200 characters).");

            // Bit stream: mode, length, bytes, terminator, padding.
            var bits = new List<bool>();
            Append(bits, 0x4, 4);
            Append(bits, data.Length, version < 10 ? 8 : 16);
            foreach (var b in data) Append(bits, b, 8);
            int capacity = DataCodewords(version) * 8;
            Append(bits, 0, Math.Min(4, capacity - bits.Count));
            Append(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11) Append(bits, pad, 8);

            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++) if (bits[i]) codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

            var all = AddEccAndInterleave(codewords, version);
            int size = version * 4 + 17;
            var modules = new bool[size, size];
            var isFunction = new bool[size, size];
            DrawFunctionPatterns(modules, isFunction, version, size);
            DrawCodewords(modules, isFunction, all, size);

            // Pick the mask with the lowest penalty.
            int bestMask = 0;
            long bestPenalty = long.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                ApplyMask(modules, isFunction, mask, size);
                DrawFormatBits(modules, isFunction, mask, size);
                long p = Penalty(modules, size);
                if (p < bestPenalty) { bestPenalty = p; bestMask = mask; }
                ApplyMask(modules, isFunction, mask, size); // undo (XOR)
            }
            ApplyMask(modules, isFunction, bestMask, size);
            DrawFormatBits(modules, isFunction, bestMask, size);
            return modules;
        }

        // ------------------------------------------------------------------ texture

        /// <summary>Saves a QR code as a crisp Sprite PNG and returns the sprite.</summary>
        public static Sprite SaveSprite(string text, string assetPath, int pixelsPerModule = 10)
        {
            var m = Encode(text);
            int size = m.GetLength(0), quiet = 4, total = (size + quiet * 2) * pixelsPerModule;
            var tex = new Texture2D(total, total, TextureFormat.RGB24, false);
            var pixels = new Color32[total * total];
            for (int y = 0; y < total; y++)
                for (int x = 0; x < total; x++)
                {
                    int mx = x / pixelsPerModule - quiet, my = y / pixelsPerModule - quiet;
                    bool dark = mx >= 0 && my >= 0 && mx < size && my < size && m[size - 1 - my, mx];
                    pixels[y * total + x] = dark ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        // ------------------------------------------------------------------ structure

        private static int RawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7) result -= 36;
            }
            return result;
        }

        private static int DataCodewords(int ver) => RawDataModules(ver) / 8 - EccPerBlock[ver] * NumBlocks[ver];

        private static int[] AlignmentPositions(int ver, int size)
        {
            if (ver == 1) return new int[0];
            int numAlign = ver / 7 + 2;
            int step = (ver * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
            var result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = size - 7; i >= 1; i--, pos -= step) result[i] = pos;
            return result;
        }

        private static void Set(bool[,] m, bool[,] f, int x, int y, bool dark)
        {
            m[y, x] = dark;
            f[y, x] = true;
        }

        private static void DrawFunctionPatterns(bool[,] m, bool[,] f, int ver, int size)
        {
            for (int i = 0; i < size; i++)
            {
                Set(m, f, 6, i, i % 2 == 0);
                Set(m, f, i, 6, i % 2 == 0);
            }
            DrawFinder(m, f, 3, 3, size);
            DrawFinder(m, f, size - 4, 3, size);
            DrawFinder(m, f, 3, size - 4, size);

            var pos = AlignmentPositions(ver, size);
            int n = pos.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            Set(m, f, pos[i] + dx, pos[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }

            DrawFormatBits(m, f, 0, size); // reserve the area
            if (ver >= 7)
            {
                int rem = ver;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bits = ver << 12 | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((bits >> i) & 1) != 0;
                    int a = size - 11 + i % 3, b = i / 3;
                    Set(m, f, a, b, bit);
                    Set(m, f, b, a, bit);
                }
            }
        }

        private static void DrawFinder(bool[,] m, bool[,] f, int x, int y, int size)
        {
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < size && yy >= 0 && yy < size) Set(m, f, xx, yy, dist != 2 && dist != 4);
                }
        }

        private static void DrawFormatBits(bool[,] m, bool[,] f, int mask, int size)
        {
            int data = FormatBitsM << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            bool Bit(int i) => ((bits >> i) & 1) != 0;

            for (int i = 0; i <= 5; i++) Set(m, f, 8, i, Bit(i));
            Set(m, f, 8, 7, Bit(6));
            Set(m, f, 8, 8, Bit(7));
            Set(m, f, 7, 8, Bit(8));
            for (int i = 9; i < 15; i++) Set(m, f, 14 - i, 8, Bit(i));
            for (int i = 0; i < 8; i++) Set(m, f, size - 1 - i, 8, Bit(i));
            for (int i = 8; i < 15; i++) Set(m, f, 8, size - 15 + i, Bit(i));
            Set(m, f, 8, size - 8, true);
        }

        private static void DrawCodewords(bool[,] m, bool[,] f, byte[] data, int size)
        {
            int i = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!f[y, x] && i < data.Length * 8)
                        {
                            m[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                            i++;
                        }
                    }
            }
        }

        private static void ApplyMask(bool[,] m, bool[,] f, int mask, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (!f[y, x] && invert) m[y, x] = !m[y, x];
                }
        }

        private static long Penalty(bool[,] m, int size)
        {
            long result = 0;
            // Runs of 5+ same-colour modules in rows and columns.
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                {
                    int run = 1;
                    for (int b = 1; b < size; b++)
                    {
                        bool cur = pass == 0 ? m[a, b] : m[b, a], prev = pass == 0 ? m[a, b - 1] : m[b - 1, a];
                        if (cur == prev) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                        else run = 1;
                    }
                }
            // 2×2 blocks.
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                    if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) result += 3;
            // Finder-like patterns 1:1:3:1:1 with light space.
            bool[] p1 = { true, false, true, true, true, false, true, false, false, false, false };
            bool[] p2 = { false, false, false, false, true, false, true, true, true, false, true };
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                    for (int b = 0; b + 11 <= size; b++)
                    {
                        bool m1 = true, m2 = true;
                        for (int k = 0; k < 11; k++)
                        {
                            bool v = pass == 0 ? m[a, b + k] : m[b + k, a];
                            if (v != p1[k]) m1 = false;
                            if (v != p2[k]) m2 = false;
                        }
                        if (m1) result += 40;
                        if (m2) result += 40;
                    }
            // Balance of dark modules.
            int dark = 0;
            foreach (var v in m) if (v) dark++;
            int total = size * size;
            int k2 = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += Math.Max(0, k2) * 10;
            return result;
        }

        // ------------------------------------------------------------------ Reed-Solomon

        private static byte[] AddEccAndInterleave(byte[] data, int ver)
        {
            int numBlocks = NumBlocks[ver], blockEccLen = EccPerBlock[ver];
            int rawCodewords = RawDataModules(ver) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;
            var divisor = RsDivisor(blockEccLen);
            var blocks = new List<byte[]>();
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                var dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                var ecc = RsRemainder(dat, divisor);
                var block = new byte[shortBlockLen + 1];
                Array.Copy(dat, block, datLen);
                Array.Copy(ecc, 0, block, block.Length - blockEccLen, blockEccLen);
                blocks.Add(block);
            }
            var result = new List<byte>();
            for (int i = 0; i < blocks[0].Length; i++)
                for (int j = 0; j < blocks.Count; j++)
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                        result.Add(blocks[j][i]);
            return result.ToArray();
        }

        private static byte[] RsDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = (byte)Mul(result[j], root);
                    if (j + 1 < result.Length) result[j] ^= result[j + 1];
                }
                root = Mul(root, 0x02);
            }
            return result;
        }

        private static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (var b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++) result[i] ^= (byte)Mul(divisor[i], factor);
            }
            return result;
        }

        private static int Mul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        private static void Append(List<bool> bits, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
        }
    }
}

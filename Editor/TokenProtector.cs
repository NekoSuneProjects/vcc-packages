#if UNITY_EDITOR
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NekoSune.Tools
{
    /// <summary>
    /// Encrypts saved VRChat session tokens with Windows DPAPI (current Windows user only).
    /// The encrypted text is useless on another PC or another Windows account.
    /// On other platforms tokens are not written to disk at all.
    /// </summary>
    internal static class TokenProtector
    {
        public static bool Available => Application.platform == RuntimePlatform.WindowsEditor;

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NekoSuneMultiPublisher.v2");
        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain) || !Available) return "";
            return Convert.ToBase64String(Run(Encoding.UTF8.GetBytes(plain), true));
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText) || !Available) return "";
            try
            {
                return Encoding.UTF8.GetString(Run(Convert.FromBase64String(protectedText), false));
            }
            catch (Exception)
            {
                return ""; // different Windows user / PC, or corrupted: treat as signed out
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob dataIn, string description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr mem);

        private static DataBlob ToBlob(byte[] data)
        {
            var blob = new DataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(data.Length) };
            Marshal.Copy(data, 0, blob.pbData, data.Length);
            return blob;
        }

        private static byte[] Run(byte[] input, bool protect)
        {
            var inBlob = ToBlob(input);
            var entropy = ToBlob(Entropy);
            var outBlob = new DataBlob();
            try
            {
                bool ok = protect
                    ? CryptProtectData(ref inBlob, "NekoSune Multi-Publisher", ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());

                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(inBlob.pbData);
                Marshal.FreeHGlobal(entropy.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }
}
#endif

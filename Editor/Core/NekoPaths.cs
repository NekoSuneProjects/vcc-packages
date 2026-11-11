using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal static class NekoPaths
    {
        public const string PackageName = "com.nekosune.lipsync";
        public const string MenuRoot = "NekoSune/";
        static string _root;
        public static string Root { get { if (!string.IsNullOrEmpty(_root) && DirectoryExists(_root)) return _root; _root = Resolve(); return _root; } }
        public static string Editor { get { return Root + "/Editor"; } }
        public static string LanguagesDir { get { return Editor + "/Localization/Languages"; } }
        public static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (Path.IsPathRooted(assetPath)) return assetPath;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
        static bool DirectoryExists(string assetPath) { string abs = ToAbsolute(assetPath); return !string.IsNullOrEmpty(abs) && Directory.Exists(abs); }
        static string Resolve()
        {
            string guess = "Packages/" + PackageName;
            if (DirectoryExists(guess)) return guess;
            string[] guids = AssetDatabase.FindAssets("NekoPaths t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(p) || !p.EndsWith("/NekoPaths.cs")) continue;
                string dir = p;
                for (int up = 0; up < 6; up++)
                {
                    int slash = dir.LastIndexOf('/');
                    if (slash < 0) break;
                    dir = dir.Substring(0, slash);
                    string abs = ToAbsolute(dir);
                    if (abs != null && File.Exists(Path.Combine(abs, "package.json"))) return dir;
                }
            }
            return "Assets/NekoSune/LipSync";
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    [Serializable]
    internal class NekoLocEntry
    {
        public string k;
        public string v;
    }

    [Serializable]
    internal class NekoLocFile
    {
        public string code;
        public string name;        // English name, e.g. "Polish"
        public string nativeName;  // e.g. "Polski"
        public List<NekoLocEntry> entries = new List<NekoLocEntry>();
    }

    internal struct NekoLanguageInfo
    {
        public string Code;
        public string Name;
        public string NativeName;
        public string FilePath;

        public string Display
        {
            get
            {
                if (!string.IsNullOrEmpty(NativeName) && NativeName != Name)
                    return NativeName + "  (" + Name + ")";
                return string.IsNullOrEmpty(NativeName) ? Name : NativeName;
            }
        }
    }

    /// <summary>
    /// JSON-file backed localization layer. Every language lives in its own file under
    /// Editor/Localization/Languages, so adding a language is just adding a file.
    /// Missing keys fall back to English, then to the raw key, so a partial translation
    /// never breaks the UI.
    /// </summary>
    internal static class NekoLoc
    {
        const string PrefKey = "NekoSune.Avatars.Language";
        const string FallbackCode = "en";

        static Dictionary<string, string> _active;
        static Dictionary<string, string> _fallback;
        static List<NekoLanguageInfo> _languages;
        static string _activeCode;

        public static event Action LanguageChanged;

        public static string ActiveCode
        {
            get { EnsureLoaded(); return _activeCode; }
        }

        public static List<NekoLanguageInfo> Languages
        {
            get { EnsureLoaded(); return _languages; }
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            EditorPrefs.SetString(PrefKey, code);
            _activeCode = code;
            _active = LoadTable(code);
            if (LanguageChanged != null) LanguageChanged();
            RepaintEverything();
        }

        public static void Reload()
        {
            _languages = null;
            _active = null;
            _fallback = null;
            EnsureLoaded();
            if (LanguageChanged != null) LanguageChanged();
            RepaintEverything();
        }

        /// <summary>Translate a key. Extra args are applied with string.Format.</summary>
        public static string T(string key, params object[] args)
        {
            EnsureLoaded();
            string val;
            if (_active == null || !_active.TryGetValue(key, out val) || string.IsNullOrEmpty(val))
            {
                if (_fallback == null || !_fallback.TryGetValue(key, out val) || string.IsNullOrEmpty(val))
                    val = key;
            }
            if (args != null && args.Length > 0)
            {
                try { val = string.Format(val, args); }
                catch (FormatException) { /* keep the raw string rather than throwing inside OnGUI */ }
            }
            return val;
        }

        /// <summary>Label plus tooltip. The tooltip key is the label key with ".tip" appended, and is optional.</summary>
        public static GUIContent C(string key)
        {
            EnsureLoaded();
            string tip = null;
            string tipKey = key + ".tip";
            if (_active != null) _active.TryGetValue(tipKey, out tip);
            if (string.IsNullOrEmpty(tip) && _fallback != null) _fallback.TryGetValue(tipKey, out tip);
            return new GUIContent(T(key), tip);
        }

        static void EnsureLoaded()
        {
            if (_languages == null) ScanLanguages();
            if (_fallback == null) _fallback = LoadTable(FallbackCode);
            if (_active == null)
            {
                _activeCode = EditorPrefs.GetString(PrefKey, null);
                if (string.IsNullOrEmpty(_activeCode) || !HasLanguage(_activeCode))
                    _activeCode = GuessSystemLanguage();
                _active = LoadTable(_activeCode);
            }
        }

        static bool HasLanguage(string code)
        {
            for (int i = 0; i < _languages.Count; i++)
                if (_languages[i].Code == code) return true;
            return false;
        }

        static string GuessSystemLanguage()
        {
            string code = SystemLanguageToCode(Application.systemLanguage);
            return HasLanguage(code) ? code : FallbackCode;
        }

        static string SystemLanguageToCode(SystemLanguage sl)
        {
            switch (sl)
            {
                case SystemLanguage.English:            return "en";
                case SystemLanguage.Russian:            return "ru";
                case SystemLanguage.Spanish:            return "es";
                case SystemLanguage.Polish:             return "pl";
                case SystemLanguage.German:             return "de";
                case SystemLanguage.French:             return "fr";
                case SystemLanguage.Italian:            return "it";
                case SystemLanguage.Portuguese:         return "pt-BR";
                case SystemLanguage.Dutch:              return "nl";
                case SystemLanguage.Japanese:           return "ja";
                case SystemLanguage.Korean:             return "ko";
                case SystemLanguage.Turkish:            return "tr";
                case SystemLanguage.Ukrainian:          return "uk";
                case SystemLanguage.Czech:              return "cs";
                case SystemLanguage.Swedish:            return "sv";
                case SystemLanguage.Finnish:            return "fi";
                case SystemLanguage.Danish:             return "da";
                case SystemLanguage.Norwegian:          return "no";
                case SystemLanguage.Hungarian:          return "hu";
                case SystemLanguage.Romanian:           return "ro";
                case SystemLanguage.Thai:               return "th";
                case SystemLanguage.Indonesian:         return "id";
                case SystemLanguage.Vietnamese:         return "vi";
                case SystemLanguage.Arabic:             return "ar";
                case SystemLanguage.Chinese:            return "zh-Hans";
                case SystemLanguage.ChineseSimplified:  return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                default:                                return "en";
            }
        }

        static void ScanLanguages()
        {
            _languages = new List<NekoLanguageInfo>();
            string dir = NekoPaths.ToAbsolute(NekoPaths.LanguagesDir);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                Debug.LogWarning("[NekoSune] Language folder not found at " + NekoPaths.LanguagesDir);
                return;
            }

            string[] files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                NekoLocFile f = ReadFile(files[i]);
                if (f == null) continue;
                NekoLanguageInfo info;
                info.Code       = string.IsNullOrEmpty(f.code) ? Path.GetFileNameWithoutExtension(files[i]) : f.code;
                info.Name       = string.IsNullOrEmpty(f.name) ? info.Code : f.name;
                info.NativeName = string.IsNullOrEmpty(f.nativeName) ? info.Name : f.nativeName;
                info.FilePath   = files[i];
                _languages.Add(info);
            }

            // English first, then alphabetical by English name.
            _languages.Sort((a, b) =>
            {
                if (a.Code == FallbackCode) return -1;
                if (b.Code == FallbackCode) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        static NekoLocFile ReadFile(string absPath)
        {
            try
            {
                string json = File.ReadAllText(absPath, System.Text.Encoding.UTF8);
                NekoLocFile f = JsonUtility.FromJson<NekoLocFile>(json);
                if (f == null) return null;
                if (f.entries == null) f.entries = new List<NekoLocEntry>();
                return f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NekoSune] Failed to read language file " + absPath + ": " + e.Message);
                return null;
            }
        }

        static Dictionary<string, string> LoadTable(string code)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_languages == null) ScanLanguages();
            for (int i = 0; i < _languages.Count; i++)
            {
                if (_languages[i].Code != code) continue;
                NekoLocFile f = ReadFile(_languages[i].FilePath);
                if (f == null) break;
                for (int e = 0; e < f.entries.Count; e++)
                {
                    NekoLocEntry en = f.entries[e];
                    if (en == null || string.IsNullOrEmpty(en.k)) continue;
                    table[en.k] = en.v;
                }
                break;
            }
            return table;
        }

        static void RepaintEverything()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
                if (windows[i] != null) windows[i].Repaint();
        }
    }
}

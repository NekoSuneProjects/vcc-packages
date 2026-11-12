using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// UI translations, same file format as NekoSune Lip Sync: Languages/&lt;code&gt;.json with
    /// { code, name, nativeName, entries: [ { k, v } ] }. The key is the English text, so anything not
    /// translated yet simply shows in English.
    /// </summary>
    internal static class Loc
    {
        [Serializable] private class Entry { public string k; public string v; }
        [Serializable] private class FileData { public string code; public string name; public string nativeName; public Entry[] entries; }

        public struct Language { public string code; public string display; public string path; }

        private const string Folder = "Packages/com.nekosune.world-ui-assistant/Editor/Localization/Languages";
        private const string Pref = "NekoSune.WorldUiAssistant.Language";

        private static List<Language> languages;
        private static Dictionary<string, string> table;
        private static string active;

        public static event Action Changed;

        public static List<Language> Languages
        {
            get
            {
                if (languages == null) Scan();
                return languages;
            }
        }

        public static string Active
        {
            get
            {
                if (active == null) active = EditorPrefs.GetString(Pref, DefaultCode());
                return active;
            }
        }

        public static void Set(string code)
        {
            if (code == Active) return;
            active = code;
            EditorPrefs.SetString(Pref, code);
            table = null;
            Changed?.Invoke();
        }

        /// <summary>Translates English UI text (returns it unchanged when there's no translation).</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english) || Active == "en") return english;
            if (table == null) Load();
            string trimmed = english.Trim();
            if (!table.TryGetValue(trimmed, out var v) || string.IsNullOrEmpty(v)) return english;
            int lead = english.Length - english.TrimStart().Length;
            int trail = english.Length - english.TrimEnd().Length;
            return english.Substring(0, lead) + v + english.Substring(english.Length - trail);
        }

        public static GUIContent C(string text, string tooltip = null) => new GUIContent(T(text), T(tooltip));

        private static void Scan()
        {
            languages = new List<Language>();
            if (!Directory.Exists(Folder)) return;
            foreach (var path in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var f = JsonUtility.FromJson<FileData>(File.ReadAllText(path, System.Text.Encoding.UTF8));
                    string code = string.IsNullOrEmpty(f.code) ? Path.GetFileNameWithoutExtension(path) : f.code;
                    languages.Add(new Language { code = code, display = string.IsNullOrEmpty(f.nativeName) ? f.name : f.nativeName, path = path });
                }
                catch (Exception e) { Debug.LogWarning("[NekoUI Assistant] Couldn't read " + path + ": " + e.Message); }
            }
            languages = languages.OrderBy(l => l.code == "en" ? "" : l.display).ToList();
        }

        private static void Load()
        {
            table = new Dictionary<string, string>();
            var lang = Languages.FirstOrDefault(l => l.code == Active);
            if (string.IsNullOrEmpty(lang.path)) return;
            try
            {
                var f = JsonUtility.FromJson<FileData>(File.ReadAllText(lang.path, System.Text.Encoding.UTF8));
                if (f.entries != null) foreach (var e in f.entries) if (!string.IsNullOrEmpty(e.k)) table[e.k] = e.v;
            }
            catch (Exception e) { Debug.LogWarning("[NekoUI Assistant] Couldn't read " + lang.path + ": " + e.Message); }
        }

        /// <summary>Starts in the editor's own language when we have it.</summary>
        private static string DefaultCode()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.German: return "de";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Portuguese: return "pt-BR";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.Chinese: return "zh-Hans";
                default: return "en";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// UI translation. Code is written in English and wrapped in L.T("..."); each language file maps the
    /// English text to its translation. Anything missing falls back to English, so a new string never breaks.
    ///
    /// To add a language: copy Localization/Languages/Lang.es.cs, translate the values, and add it to <see cref="Languages"/>.
    /// </summary>
    public static class L
    {
        public sealed class Language
        {
            public readonly string code, nativeName;
            public readonly SystemLanguage[] system;
            public readonly Dictionary<string, string> table; // null = English

            public Language(string code, string nativeName, Dictionary<string, string> table, params SystemLanguage[] system)
            {
                this.code = code; this.nativeName = nativeName; this.table = table; this.system = system;
            }
        }

        private const string Pref = "MotionConverter.Language";

        public static readonly Language[] Languages =
        {
            new Language("en", "English", null, SystemLanguage.English),
            new Language("es", "Español", Translations.Spanish, SystemLanguage.Spanish),
            new Language("ru", "Русский", Translations.Russian, SystemLanguage.Russian, SystemLanguage.Ukrainian, SystemLanguage.Belarusian),
            new Language("pl", "Polski", Translations.Polish, SystemLanguage.Polish),
            new Language("de", "Deutsch", Translations.German, SystemLanguage.German),
            new Language("fr", "Français", Translations.French, SystemLanguage.French),
            new Language("pt-BR", "Português (Brasil)", Translations.PortugueseBR, SystemLanguage.Portuguese),
            new Language("ja", "日本語", Translations.Japanese, SystemLanguage.Japanese),
            new Language("ko", "한국어", Translations.Korean, SystemLanguage.Korean),
            new Language("zh-Hans", "简体中文", Translations.ChineseSimplified, SystemLanguage.ChineseSimplified, SystemLanguage.Chinese),
        };

        private static Language current;

        /// <summary>Selected language. First run: the system language if available, otherwise English.</summary>
        public static Language Current
        {
            get
            {
                if (current != null) return current;
                string code = EditorPrefs.GetString(Pref, "");
                current = Languages.FirstOrDefault(l => l.code == code)
                       ?? Languages.FirstOrDefault(l => l.system.Contains(Application.systemLanguage))
                       ?? Languages[0];
                return current;
            }
            set
            {
                if (value == null) return;
                current = value;
                EditorPrefs.SetString(Pref, value.code);
            }
        }

        /// <summary>Translate English text.</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            var table = Current.table;
            return table != null && table.TryGetValue(english, out string s) && !string.IsNullOrEmpty(s) ? s : english;
        }

        /// <summary>Translate an English format string ("Saved: {0}") and fill it in.</summary>
        public static string T(string english, params object[] args)
        {
            try { return string.Format(T(english), args); }
            catch (FormatException) { return string.Format(english, args); } // broken translation: fall back
        }

        /// <summary>Translated label with optional translated tooltip.</summary>
        public static GUIContent C(string text, string tooltip = null) =>
            new GUIContent(T(text), tooltip == null ? null : T(tooltip));

        /// <summary>Language dropdown. Returns true when the language changed.</summary>
        public static bool Popup(Rect rect)
        {
            int index = Array.IndexOf(Languages, Current);
            int picked = EditorGUI.Popup(rect, index, Languages.Select(l => l.nativeName).ToArray());
            if (picked == index || picked < 0) return false;
            Current = Languages[picked];
            return true;
        }
    }

    /// <summary>Translation tables, one partial file per language under Localization/Languages.</summary>
    internal static partial class Translations { }
}

using System.Linq;
using UnityEditor;
using UnityEngine;
using S = MotionConverter.MotionConverterStyles;

namespace MotionConverter
{
    /// <summary>Shared card layout and widgets, so every module looks and behaves the same.</summary>
    internal static class MotionConverterGUI
    {
        public static void BeginCard(string icon, string title)
        {
            S.Ensure();
            EditorGUILayout.BeginVertical(S.Card);
            if (string.IsNullOrEmpty(title)) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(icon)) GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(title, S.CardTitle);
            }
            S.Line();
            EditorGUILayout.Space(6);
        }

        public static void EndCard() => EditorGUILayout.EndVertical();

        public static void Chip(string text) => GUILayout.Label(text, S.Chip, GUILayout.ExpandWidth(false));

        /// <summary>A row of chips aligned with the field values above them.</summary>
        public static void ChipRow(params string[] chips)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                foreach (var c in chips) Chip(c);
                GUILayout.FlexibleSpace();
            }
        }

        public static void Legend(Color color, string text)
        {
            var swatch = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10), GUILayout.Height(16));
            EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 4, 8, 8), color);
            GUILayout.Label(text, S.PathLabel, GUILayout.ExpandWidth(false));
            GUILayout.Space(6);
        }

        /// <summary>
        /// Warning card with an "I understand" tick box. The tick is remembered per machine under <paramref name="prefKey"/>.
        /// Returns whether it is ticked.
        /// </summary>
        public static bool Disclaimer(string title, string text, string prefKey)
        {
            BeginCard("console.warnicon", title);
            EditorGUILayout.HelpBox(text, MessageType.Warning);
            bool accepted = IsAccepted(prefKey);
            bool now = EditorGUILayout.ToggleLeft(L.T("I have read and understand this disclaimer"), accepted, EditorStyles.boldLabel);
            if (now != accepted) EditorPrefs.SetBool(prefKey, now);
            EndCard();
            return now;
        }

        public static bool IsAccepted(string prefKey) => EditorPrefs.GetBool(prefKey, false);

        /// <summary>Artist + Title fields used by every module. Output goes to [Motions Converted]/Artist/Title.</summary>
        public static void NamingFields(ref string artist, ref string title)
        {
            artist = EditorGUILayout.TextField(L.C("Artist", "e.g. Ironmouse"), artist);
            title = EditorGUILayout.TextField(L.C("Title", "e.g. Cry For Me"), title);
        }

        /// <summary>Folder icon + the path(s) the clip(s) will be saved to.</summary>
        public static void OutputPaths(params string[] paths)
        {
            foreach (var path in paths.Where(p => !string.IsNullOrEmpty(p)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(18), GUILayout.Height(16));
                    GUILayout.Label(path, S.PathLabel);
                }
            }
        }

        public static bool BigButton(string text, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = enabled ? S.Accent : old;
                bool clicked = GUILayout.Button(S.Icon("PlayButton", "  " + text), S.BigButton);
                GUI.backgroundColor = old;
                return clicked;
            }
        }

        /// <summary>Result line with Ping / Select buttons.</summary>
        public static void ResultRow(Object result, bool failed)
        {
            if (result == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3, 20, GUILayout.Width(3));
                EditorGUI.DrawRect(bar, failed ? S.Error : S.Success);
                GUILayout.Label(S.Icon(failed ? "console.erroricon.sml" : "TestPassed", " " + result.name), GUILayout.Height(20));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L.T("Ping"), EditorStyles.miniButtonLeft, GUILayout.MinWidth(48))) EditorGUIUtility.PingObject(result);
                if (GUILayout.Button(L.T("Select"), EditorStyles.miniButtonRight, GUILayout.MinWidth(52))) Selection.activeObject = result;
            }
        }

        public static void LogCard(ConversionLog log, ref bool show)
        {
            if (log == null || log.Entries.Count == 0) return;
            BeginCard(null, null);

            using (new EditorGUILayout.HorizontalScope())
            {
                show = EditorGUILayout.Foldout(show, L.T("Log  ({0})", log.Entries.Count), true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L.T("Copy"), EditorStyles.miniButtonLeft, GUILayout.MinWidth(48)))
                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", log.Entries.Select(e => $"[{e.level}] {e.text}"));
                if (GUILayout.Button(L.T("Clear"), EditorStyles.miniButtonRight, GUILayout.MinWidth(48)))
                    log.Clear();
            }

            if (show)
            {
                EditorGUILayout.Space(4);
                foreach (var (level, text) in log.Entries)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string icon = level == LogLevel.Error ? "console.erroricon.sml"
                            : level == LogLevel.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
                        GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                        GUILayout.Label(text, S.LogText);
                    }
                }
            }

            EndCard();
        }
    }
}

using UnityEditor;
using UnityEngine;
using S = NekoSune.LipSync.Editor.LipSyncStyles;

namespace NekoSune.LipSync.Editor
{
    /// <summary>Shared card layout and widgets (same look as Motion Converter).</summary>
    internal static class LipSyncGUI
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

        /// <summary>Card whose title is a foldout. Returns whether it is open; always call <see cref="EndCard"/>.</summary>
        public static bool BeginFoldoutCard(string icon, string title, bool open)
        {
            S.Ensure();
            EditorGUILayout.BeginVertical(S.Card);
            // Arrow first, then icon + title, so the row reads like a normal card title.
            var content = string.IsNullOrEmpty(icon)
                ? new GUIContent(" " + title)
                : new GUIContent(" " + title, EditorGUIUtility.IconContent(icon).image);
            open = EditorGUILayout.Foldout(open, content, true, FoldoutTitle);
            if (open)
            {
                S.Line();
                EditorGUILayout.Space(6);
            }
            return open;
        }

        static GUIStyle _foldoutTitle;
        static GUIStyle FoldoutTitle
        {
            get
            {
                if (_foldoutTitle == null)
                    _foldoutTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12, fixedHeight = 20 };
                return _foldoutTitle;
            }
        }

        public static void EndCard() => EditorGUILayout.EndVertical();

        public static void Chip(string text, string tooltip = null) =>
            GUILayout.Label(new GUIContent(text, tooltip), S.Chip, GUILayout.ExpandWidth(false));

        /// <summary>A chip with coloured text, e.g. green for mapped / red for missing.</summary>
        public static void Chip(string text, Color textColor, string tooltip = null)
        {
            var style = new GUIStyle(S.Chip);
            style.normal.textColor = textColor;
            GUILayout.Label(new GUIContent(text, tooltip), style, GUILayout.ExpandWidth(false));
        }

        /// <summary>A row of chips aligned with the field values above them.</summary>
        public static void ChipRow(params string[] chips)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                foreach (var c in chips) if (!string.IsNullOrEmpty(c)) Chip(c);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Small muted sub-heading inside a card.</summary>
        public static void Group(string text, bool first = false)
        {
            if (!first) EditorGUILayout.Space(6);
            GUILayout.Label(text.ToUpperInvariant(), S.GroupLabel);
        }

        /// <summary>Folder icon + a path.</summary>
        public static void OutputPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(18), GUILayout.Height(16));
                GUILayout.Label(path, S.PathLabel);
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
        public static void ResultRow(Object result, string detail)
        {
            if (result == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3, 20, GUILayout.Width(3));
                EditorGUI.DrawRect(bar, S.Success);
                GUILayout.Label(S.Icon("TestPassed", " " + result.name), GUILayout.Height(20));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(NekoLoc.T("result.ping"), EditorStyles.miniButtonLeft, GUILayout.MinWidth(48))) EditorGUIUtility.PingObject(result);
                if (GUILayout.Button(NekoLoc.T("result.select"), EditorStyles.miniButtonRight, GUILayout.MinWidth(52))) Selection.activeObject = result;
            }
            if (!string.IsNullOrEmpty(detail)) EditorGUILayout.LabelField(detail, S.PathLabel);
        }

        /// <summary>Red bar + error text.</summary>
        public static void ErrorRow(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3, 20, GUILayout.Width(3), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(bar, S.Error);
                GUILayout.Label(EditorGUIUtility.IconContent("console.erroricon.sml"), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(message, EditorStyles.wordWrappedLabel);
            }
        }
    }
}

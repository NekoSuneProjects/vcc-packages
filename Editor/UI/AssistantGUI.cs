using UnityEditor;
using UnityEngine;
using S = NekoSune.WorldUiAssistant.AssistantStyles;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>Shared card layout and widgets for the UI Assistant panels.</summary>
    internal static class AssistantGUI
    {
        /// <summary>Card a lesson points at (matched against the card's key or title); drawn with an accent outline.</summary>
        public static string Highlight;
        private static readonly System.Collections.Generic.Stack<bool> highlighted = new System.Collections.Generic.Stack<bool>();

        public static void BeginCard(string icon, string title, string subtitle = null, string key = null)
        {
            S.Ensure();
            string id = key ?? title;
            highlighted.Push(!string.IsNullOrEmpty(Highlight) && !string.IsNullOrEmpty(id) && id.StartsWith(Highlight));
            EditorGUILayout.BeginVertical(S.Card);
            if (string.IsNullOrEmpty(title)) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(icon)) GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(title, S.CardTitle);
                GUILayout.FlexibleSpace();
            }
            if (!string.IsNullOrEmpty(subtitle) && Help.ShowHints) GUILayout.Label(subtitle, S.Hint);
            S.Line();
            EditorGUILayout.Space(6);
        }

        public static void EndCard()
        {
            EditorGUILayout.EndVertical();
            bool on = highlighted.Count > 0 && highlighted.Pop();
            if (on && Event.current.type == EventType.Repaint)
            {
                var r = GUILayoutUtility.GetLastRect();
                var c = S.Warning;
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), c);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), c);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), c);
                EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), c);
            }
        }

        /// <summary>Explain mode: names the Unity component/property a field changes.</summary>
        public static void Api(string api)
        {
            if (!Help.Explain) return;
            GUILayout.Label("↳ Unity: " + api, ApiStyle);
        }

        private static GUIStyle apiStyle;
        private static GUIStyle ApiStyle
        {
            get
            {
                if (apiStyle == null)
                {
                    apiStyle = new GUIStyle(EditorStyles.miniLabel) { padding = new RectOffset((int)EditorGUIUtility.labelWidth / 3, 0, 0, 2) };
                    apiStyle.normal.textColor = new Color(0.45f, 0.75f, 1f);
                }
                return apiStyle;
            }
        }

        private static GUIStyle foldoutTitle;
        public static GUIStyle FoldoutTitle =>
            foldoutTitle ?? (foldoutTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12, fixedHeight = 20 });

        public static void Chip(string text, string tooltip = null) =>
            GUILayout.Label(new GUIContent(text, tooltip), S.Chip, GUILayout.ExpandWidth(false));

        public static void Chip(string text, Color textColor, string tooltip = null)
        {
            var style = new GUIStyle(S.Chip);
            style.normal.textColor = textColor;
            GUILayout.Label(new GUIContent(text, tooltip), style, GUILayout.ExpandWidth(false));
        }

        /// <summary>Small muted sub-heading inside a card.</summary>
        public static void Group(string text, bool first = false)
        {
            if (!first) EditorGUILayout.Space(6);
            GUILayout.Label(text.ToUpperInvariant(), S.GroupLabel);
        }

        public static void Hint(string text)
        {
            if (Help.ShowHints) GUILayout.Label(text, S.Hint);
        }

        /// <summary>Full-width action button; accent-tinted when <paramref name="primary"/>.</summary>
        public static bool Action(string icon, string text, bool enabled = true, bool primary = false, string tooltip = null)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var old = GUI.backgroundColor;
                if (primary && enabled) GUI.backgroundColor = S.Accent;
                var content = string.IsNullOrEmpty(icon) ? new GUIContent(text, tooltip) : S.Icon(icon, "  " + text, tooltip);
                bool clicked = GUILayout.Button(content, S.BigButton);
                GUI.backgroundColor = old;
                return clicked;
            }
        }

        /// <summary>Bar + message; green for a result, red for a problem.</summary>
        public static void Report(string message, bool ok)
        {
            if (string.IsNullOrEmpty(message)) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3, 20, GUILayout.Width(3), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(bar, ok ? S.Success : S.Error);
                GUILayout.Label(EditorGUIUtility.IconContent(ok ? "TestPassed" : "console.warnicon.sml"), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(message, EditorStyles.wordWrappedLabel);
            }
        }
    }
}

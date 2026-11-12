using UnityEditor;
using UnityEngine;
using S = NekoSune.WorldUiAssistant.AssistantStyles;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>Shared card layout and widgets for the UI Assistant panels.</summary>
    internal static class AssistantGUI
    {
        public static void BeginCard(string icon, string title, string subtitle = null)
        {
            S.Ensure();
            EditorGUILayout.BeginVertical(S.Card);
            if (string.IsNullOrEmpty(title)) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(icon)) GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(title, S.CardTitle);
                GUILayout.FlexibleSpace();
            }
            if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, S.Hint);
            S.Line();
            EditorGUILayout.Space(6);
        }

        public static void EndCard() => EditorGUILayout.EndVertical();

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

        public static void Hint(string text) => GUILayout.Label(text, S.Hint);

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

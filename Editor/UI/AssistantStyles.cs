using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Colours, styles and small drawing helpers for the UI Assistant window. Built lazily and
    /// rebuilt after an editor skin change.
    /// </summary>
    internal static class AssistantStyles
    {
        private static bool builtForPro;
        private static bool built;

        public static Color Accent => new Color(0.36f, 0.56f, 1.00f);
        public static Color Success => new Color(0.30f, 0.75f, 0.45f);
        public static Color Warning => new Color(0.95f, 0.70f, 0.25f);
        public static Color Error => new Color(0.92f, 0.36f, 0.36f);

        public static Color HeaderBg => Pro ? new Color(0.13f, 0.14f, 0.17f) : new Color(0.80f, 0.82f, 0.86f);
        public static Color CardBg => Pro ? new Color(0.20f, 0.20f, 0.22f) : new Color(0.88f, 0.88f, 0.89f);
        public static Color CardBorder => Pro ? new Color(0.11f, 0.11f, 0.12f) : new Color(0.68f, 0.68f, 0.70f);
        public static Color TrackBg => Pro ? new Color(0.14f, 0.14f, 0.15f) : new Color(0.74f, 0.74f, 0.76f);
        public static Color RowBg => Pro ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.78f, 0.78f, 0.78f);
        public static Color Muted => Pro ? new Color(0.62f, 0.64f, 0.68f) : new Color(0.35f, 0.36f, 0.40f);
        public static Color Text => Pro ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.09f, 0.09f, 0.09f);

        public static GUIStyle Title, Subtitle, CardTitle, Card, Chip, PathLabel, BigButton, GroupLabel, Tab, TabActive, RuleCard, Hint;

        private static bool Pro => EditorGUIUtility.isProSkin;

        public static void Ensure()
        {
            if (built && builtForPro == Pro && Card?.normal.background != null) return;
            built = true;
            builtForPro = Pro;

            Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            Title.normal.textColor = Pro ? Color.white : new Color(0.1f, 0.1f, 0.12f);

            Subtitle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            Subtitle.normal.textColor = Muted;

            CardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            Card = new GUIStyle
            {
                padding = new RectOffset(12, 12, 10, 12),
                margin = new RectOffset(8, 8, 4, 6),
                border = new RectOffset(1, 1, 1, 1)
            };
            Card.normal.background = BorderedTexture(CardBg, CardBorder);

            RuleCard = new GUIStyle
            {
                padding = new RectOffset(8, 8, 6, 8),
                margin = new RectOffset(0, 0, 3, 3),
                border = new RectOffset(1, 1, 1, 1)
            };
            RuleCard.normal.background = BorderedTexture(TrackBg, CardBorder);

            Chip = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 2, 2),
                margin = new RectOffset(0, 4, 2, 2),
                border = new RectOffset(1, 1, 1, 1)
            };
            Chip.normal.background = BorderedTexture(TrackBg, CardBorder);
            Chip.normal.textColor = Muted;

            PathLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            PathLabel.normal.textColor = Muted;

            Hint = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            Hint.normal.textColor = Muted;

            BigButton = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 28 };

            GroupLabel = new GUIStyle(EditorStyles.miniBoldLabel);
            GroupLabel.normal.textColor = Muted;

            Tab = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(10, 10, 4, 4),
                imagePosition = ImagePosition.ImageLeft
            };
            Tab.normal.textColor = Muted;
            Tab.hover.textColor = Text;

            TabActive = new GUIStyle(Tab) { fontStyle = FontStyle.Bold };
            TabActive.normal.textColor = Text;
            TabActive.hover.textColor = Text;
        }

        /// <summary>Solid fill with a 1px border, for 9-slicing via GUIStyle.border.</summary>
        private static Texture2D BorderedTexture(Color fill, Color border)
        {
            var tex = new Texture2D(3, 3) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                tex.SetPixel(x, y, x == 1 && y == 1 ? fill : border);
            tex.Apply();
            return tex;
        }

        public static void Line(float height = 1f)
        {
            var r = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, CardBorder);
        }

        public static GUIContent Icon(string name, string text = null, string tooltip = null)
        {
            return new GUIContent(EditorGUIUtility.IconContent(name)) { text = text, tooltip = tooltip };
        }
    }
}

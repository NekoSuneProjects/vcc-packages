using System;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    internal enum UiThemeId { NekoDark, Light, Neon, Glass, Pastel, Terminal }

    /// <summary>Colours and sizes used when the Assistant creates or re-colours UI.</summary>
    [Serializable]
    internal class UiTheme
    {
        public string name;
        public Color background;   // panel background
        public Color control;      // toggle boxes, slider tracks, image placeholders
        public Color primary;      // buttons
        public Color accent;       // checkmarks, slider fills, highlights
        public Color text;
        public Color mutedText;
        public Color buttonText = Color.white;
        public int fontSize = 26;
        public float spacing = 12f;
        public float padding = 24f;
        public float buttonHeight = 64f;

        public static readonly UiThemeId[] All =
            { UiThemeId.NekoDark, UiThemeId.Light, UiThemeId.Neon, UiThemeId.Glass, UiThemeId.Pastel, UiThemeId.Terminal };

        public static string DisplayName(UiThemeId id)
        {
            switch (id)
            {
                case UiThemeId.NekoDark: return "Neko Dark";
                default: return id.ToString();
            }
        }

        public static UiTheme Get(UiThemeId id)
        {
            var t = new UiTheme { name = DisplayName(id) };
            switch (id)
            {
                case UiThemeId.Light:
                    t.background = new Color(0.94f, 0.95f, 0.98f, 0.98f);
                    t.control = new Color(0.80f, 0.83f, 0.89f, 1f);
                    t.primary = new Color(0.15f, 0.42f, 0.78f, 1f);
                    t.accent = new Color(0.12f, 0.62f, 0.45f, 1f);
                    t.text = new Color(0.07f, 0.08f, 0.11f, 1f);
                    t.mutedText = new Color(0.32f, 0.36f, 0.44f, 1f);
                    break;
                case UiThemeId.Neon:
                    t.background = new Color(0.03f, 0.03f, 0.065f, 0.98f);
                    t.control = new Color(0.08f, 0.10f, 0.16f, 1f);
                    t.primary = new Color(0.62f, 0.12f, 0.90f, 1f);
                    t.accent = new Color(0.05f, 0.95f, 0.82f, 1f);
                    t.text = new Color(0.94f, 0.98f, 1f, 1f);
                    t.mutedText = new Color(0.65f, 0.72f, 0.85f, 1f);
                    break;
                case UiThemeId.Glass:
                    t.background = new Color(0.06f, 0.09f, 0.13f, 0.70f);
                    t.control = new Color(0.20f, 0.25f, 0.32f, 0.80f);
                    t.primary = new Color(0.22f, 0.57f, 0.92f, 0.88f);
                    t.accent = new Color(0.32f, 0.85f, 0.70f, 0.92f);
                    t.text = Color.white;
                    t.mutedText = new Color(0.76f, 0.82f, 0.90f, 1f);
                    break;
                case UiThemeId.Pastel:
                    t.background = new Color(0.20f, 0.18f, 0.27f, 0.98f);
                    t.control = new Color(0.33f, 0.29f, 0.40f, 1f);
                    t.primary = new Color(0.86f, 0.48f, 0.70f, 1f);
                    t.accent = new Color(0.55f, 0.88f, 0.78f, 1f);
                    t.text = new Color(1f, 0.96f, 0.99f, 1f);
                    t.mutedText = new Color(0.86f, 0.76f, 0.85f, 1f);
                    break;
                case UiThemeId.Terminal:
                    t.background = new Color(0.015f, 0.03f, 0.02f, 0.99f);
                    t.control = new Color(0.04f, 0.09f, 0.055f, 1f);
                    t.primary = new Color(0.05f, 0.42f, 0.18f, 1f);
                    t.accent = new Color(0.20f, 1f, 0.42f, 1f);
                    t.text = new Color(0.50f, 1f, 0.62f, 1f);
                    t.mutedText = new Color(0.30f, 0.70f, 0.40f, 1f);
                    t.buttonText = new Color(0.80f, 1f, 0.85f, 1f);
                    t.spacing = 8f;
                    t.fontSize = 24;
                    break;
                default:
                    t.background = new Color(0.07f, 0.08f, 0.105f, 0.97f);
                    t.control = new Color(0.16f, 0.18f, 0.24f, 1f);
                    t.primary = new Color(0.20f, 0.44f, 0.76f, 1f);
                    t.accent = new Color(0.32f, 0.78f, 0.58f, 1f);
                    t.text = Color.white;
                    t.mutedText = new Color(0.72f, 0.76f, 0.84f, 1f);
                    break;
            }
            return t;
        }
    }
}

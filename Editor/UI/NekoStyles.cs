using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldGallery.Editor
{
    /// <summary>
    /// Shared look-and-feel for NekoSune World editor windows.
    /// Kept visually in sync with the Avatar Hub while preserving the older
    /// World style aliases used by existing addon packages.
    /// </summary>
    internal static class NekoStyles
    {
        static bool _built;

        public static GUIStyle Title;
        public static GUIStyle TitleAccent;
        public static GUIStyle Subtitle;
        public static GUIStyle Card;
        public static GUIStyle CardHover;
        public static GUIStyle Banner;
        public static GUIStyle SlotName;
        public static GUIStyle SlotMeta;
        public static GUIStyle Link;
        public static GUIStyle PrimaryButton;
        public static GUIStyle Chip;
        public static GUIStyle SectionHeader;
        public static GUIStyle NumberField;
        public static GUIStyle IconBig;
        public static GUIStyle WrapLabel;

        static Texture2D _cardTex, _cardHoverTex, _bannerTex, _chipTex, _primaryTex, _primaryHoverTex, _white;

        public static readonly Color Accent     = new Color(0.29f, 0.56f, 0.98f);
        public static readonly Color AccentSoft = new Color(0.29f, 0.56f, 0.98f, 0.25f);
        public static readonly Color Good       = new Color(0.34f, 0.80f, 0.44f);
        public static readonly Color Warn       = new Color(0.95f, 0.72f, 0.25f);
        public static readonly Color Bad        = new Color(0.92f, 0.36f, 0.36f);
        public static readonly Color Dim        = new Color(0.62f, 0.64f, 0.69f);
        public static readonly Color WaveColor  = new Color(0.42f, 0.66f, 1.00f);

        public static bool IsDark { get { return EditorGUIUtility.isProSkin; } }

        // Backwards-compatible aliases used by older World addon windows.
        public static GUIStyle HeaderTitle { get { Ensure(); return Title; } }
        public static GUIStyle CardTitle { get { Ensure(); return SlotName; } }
        public static GUIStyle CardDescription { get { Ensure(); return WrapLabel; } }

        public static Texture2D White
        {
            get
            {
                if (_white == null) _white = Solid(Color.white);
                return _white;
            }
        }

        public static void Ensure()
        {
            if (_built && Title != null && _cardTex != null) return;
            _built = true;

            bool dark = IsDark;
            Color cardBg      = dark ? new Color(0.18f, 0.19f, 0.21f) : new Color(0.87f, 0.88f, 0.90f);
            Color cardBgHover = dark ? new Color(0.22f, 0.24f, 0.28f) : new Color(0.92f, 0.93f, 0.96f);
            Color bannerBg    = dark ? new Color(0.11f, 0.22f, 0.14f) : new Color(0.83f, 0.94f, 0.85f);
            Color chipBg      = dark ? new Color(0.24f, 0.25f, 0.28f) : new Color(0.80f, 0.81f, 0.84f);
            Color text        = dark ? new Color(0.87f, 0.88f, 0.90f) : new Color(0.13f, 0.13f, 0.14f);

            _cardTex         = Solid(cardBg);
            _cardHoverTex    = Solid(cardBgHover);
            _bannerTex       = Solid(bannerBg);
            _chipTex         = Solid(chipBg);
            _primaryTex      = Solid(Accent);
            _primaryHoverTex = Solid(new Color(Accent.r * 1.12f, Accent.g * 1.12f, Accent.b * 1.12f));

            Title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };
            Title.normal.textColor = text;

            TitleAccent = new GUIStyle(Title);
            TitleAccent.normal.textColor = Accent;

            Subtitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = false
            };
            Subtitle.normal.textColor = Dim;

            WrapLabel = new GUIStyle(EditorStyles.label) { wordWrap = true };
            WrapLabel.normal.textColor = Dim;

            Card = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 4)
            };
            Card.normal.background = _cardTex;

            CardHover = new GUIStyle(Card);
            CardHover.normal.background = _cardHoverTex;

            Banner = new GUIStyle(Card);
            Banner.normal.background = _bannerTex;

            SlotName = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            SlotName.normal.textColor = text;

            SlotMeta = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            SlotMeta.normal.textColor = Dim;

            Link = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            Link.normal.textColor = Accent;
            Link.hover.textColor = Accent;

            PrimaryButton = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                padding = new RectOffset(18, 18, 8, 8)
            };
            PrimaryButton.normal.background = _primaryTex;
            PrimaryButton.hover.background = _primaryHoverTex;
            PrimaryButton.active.background = _primaryHoverTex;
            PrimaryButton.focused.background = _primaryTex;
            PrimaryButton.normal.textColor = Color.white;
            PrimaryButton.hover.textColor = Color.white;
            PrimaryButton.active.textColor = Color.white;
            PrimaryButton.focused.textColor = Color.white;

            Chip = new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(7, 7, 2, 2),
                margin = new RectOffset(0, 4, 0, 0),
                alignment = TextAnchor.MiddleCenter
            };
            Chip.normal.background = _chipTex;
            Chip.normal.textColor = Dim;

            SectionHeader = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            SectionHeader.normal.textColor = Accent;
            SectionHeader.onNormal.textColor = Accent;
            SectionHeader.focused.textColor = Accent;
            SectionHeader.onFocused.textColor = Accent;

            NumberField = new GUIStyle(EditorStyles.numberField) { alignment = TextAnchor.MiddleLeft };

            IconBig = new GUIStyle(EditorStyles.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            IconBig.normal.textColor = Dim;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        public static void Rule(float pad = 6f)
        {
            GUILayout.Space(pad);
            Rect r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, IsDark ? new Color(1, 1, 1, 0.07f) : new Color(0, 0, 0, 0.10f));
            GUILayout.Space(pad);
        }

        public static void Outline(Rect r, Color c, float thickness = 1f)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        public static float SliderRow(GUIContent label, float value, float min, float max, string format = "0.##", float labelWidth = 190f)
        {
            Ensure();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            string text = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            string edited = EditorGUILayout.TextField(text, NumberField, GUILayout.Width(56f));
            if (edited != text)
            {
                float parsed;
                if (float.TryParse(edited, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out parsed))
                    value = Mathf.Clamp(parsed, min, max);
            }
            EditorGUILayout.EndHorizontal();
            return value;
        }

        public static int IntSliderRow(GUIContent label, int value, int min, int max, float labelWidth = 190f)
        {
            return Mathf.RoundToInt(SliderRow(label, value, min, max, "0", labelWidth));
        }

        public static bool ToggleRow(GUIContent label, bool value, float labelWidth = 190f)
        {
            Ensure();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
            value = EditorGUILayout.Toggle(value, GUILayout.Width(18f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return value;
        }

        public static void HeaderBar(string titlePlain, string titleAccented, string subtitle)
        {
            Ensure();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(2f);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(titleAccented))
            {
                GUILayout.Label(titleAccented, TitleAccent, GUILayout.Height(32f));
                GUILayout.Space(6f);
            }
            GUILayout.Label(titlePlain, Title, GUILayout.Height(32f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(subtitle))
                GUILayout.Label(subtitle, Subtitle);
            GUILayout.Space(6f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // Legacy World helper kept so existing addon windows remain source-compatible.
        public static void Header(string title, string subtitle)
        {
            HeaderBar(title, null, subtitle);
        }
    }
}

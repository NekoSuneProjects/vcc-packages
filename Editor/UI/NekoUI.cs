#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.EyeInstaller.UI
{
    /// <summary>
    /// Shared visual language for the NekoSuneVR installer windows.
    ///
    /// Everything is drawn rather than skinned, so the windows look identical in both the light
    /// and dark editor themes instead of inheriting Unity's default grey. Rounded panels use the
    /// borderRadius overload of GUI.DrawTexture (Unity 2019.3+), which gives real rounded corners
    /// without shipping any sprite assets.
    /// </summary>
    public static class NekoUI
    {
        // Palette
        public static readonly Color Background = new Color(0.086f, 0.086f, 0.106f);
        public static readonly Color Card = new Color(0.125f, 0.125f, 0.153f);
        public static readonly Color CardHover = new Color(0.157f, 0.153f, 0.192f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color TextPrimary = new Color(0.93f, 0.93f, 0.96f);
        public static readonly Color TextMuted = new Color(0.60f, 0.60f, 0.68f);

        public static readonly Color AccentA = new Color(0.659f, 0.333f, 0.969f); // violet
        public static readonly Color AccentB = new Color(0.925f, 0.282f, 0.600f); // pink
        public static readonly Color Good = new Color(0.30f, 0.82f, 0.52f);
        public static readonly Color Warn = new Color(0.98f, 0.70f, 0.24f);
        public static readonly Color Bad = new Color(0.95f, 0.35f, 0.40f);

        // Brand colours for the social bar
        public static readonly Color BrandX = new Color(0.07f, 0.07f, 0.08f);
        public static readonly Color BrandTwitch = new Color(0.569f, 0.275f, 1f);
        public static readonly Color BrandYouTube = new Color(1f, 0f, 0f);

        public const string UrlX = "https://x.com/NekoSuneVR";
        public const string UrlTwitch = "https://twitch.tv/NekoSuneVR";
        public const string UrlYouTube = "https://youtube.com/@NekoSuneVR";

        private static readonly Dictionary<string, Texture2D> GradientCache = new Dictionary<string, Texture2D>();

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        /// <summary>Filled rounded rectangle, optionally with an outline.</summary>
        public static void RoundedBox(Rect rect, Color fill, float radius, float borderWidth = 0f, Color border = default(Color))
        {
            if (Event.current.type != EventType.Repaint) return;

            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                fill, Vector4.zero, Vector4.one * radius);

            if (borderWidth > 0f)
            {
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                    border, Vector4.one * borderWidth, Vector4.one * radius);
            }
        }

        /// <summary>Horizontal two-stop gradient, cached per colour pair.</summary>
        public static Texture2D GradientTexture(Color from, Color to)
        {
            string key = from + "|" + to;

            Texture2D texture;
            if (GradientCache.TryGetValue(key, out texture) && texture != null)
                return texture;

            const int width = 128;
            texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < width; x++)
                texture.SetPixel(x, 0, Color.Lerp(from, to, x / (float)(width - 1)));

            texture.Apply();
            GradientCache[key] = texture;
            return texture;
        }

        public static void GradientBox(Rect rect, Color from, Color to, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;

            GUI.DrawTexture(rect, GradientTexture(from, to), ScaleMode.StretchToFill, true, 0f,
                Color.white, Vector4.zero, Vector4.one * radius);
        }

        // ------------------------------------------------------------------
        // Styles
        // ------------------------------------------------------------------

        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle bodyStyle;
        private static GUIStyle mutedStyle;
        private static GUIStyle badgeStyle;

        public static GUIStyle Title
        {
            get
            {
                return titleStyle ?? (titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 19,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(0, 0, 0, 0)
                });
            }
        }

        public static GUIStyle Subtitle
        {
            get
            {
                return subtitleStyle ?? (subtitleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.78f) },
                    wordWrap = true
                });
            }
        }

        public static GUIStyle Section
        {
            get
            {
                return sectionStyle ?? (sectionStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = TextPrimary }
                });
            }
        }

        public static GUIStyle Body
        {
            get
            {
                return bodyStyle ?? (bodyStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = TextPrimary }
                });
            }
        }

        public static GUIStyle Muted
        {
            get
            {
                return mutedStyle ?? (mutedStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                    normal = { textColor = TextMuted }
                });
            }
        }

        public static GUIStyle Badge
        {
            get
            {
                return badgeStyle ?? (badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                });
            }
        }

        // ------------------------------------------------------------------
        // Composite widgets
        // ------------------------------------------------------------------

        /// <summary>Gradient header banner with a title, a subtitle and an optional version badge.</summary>
        public static void Header(string title, string subtitle, string version = null)
        {
            Rect rect = GUILayoutUtility.GetRect(0, int.MaxValue, 62f, 62f);
            rect = Inset(rect, 8f, 8f, 8f, 2f);

            GradientBox(rect, AccentA, AccentB, 10f);

            // Soft inner shade at the bottom so white text stays readable over the light pink end.
            RoundedBox(new Rect(rect.x, rect.y + rect.height * 0.55f, rect.width, rect.height * 0.45f),
                new Color(0f, 0f, 0f, 0.13f), 10f);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 9f, rect.width - 130f, 24f), title, Title);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 32f, rect.width - 130f, 24f), subtitle, Subtitle);

            if (!string.IsNullOrEmpty(version))
            {
                var badge = new Rect(rect.xMax - 78f, rect.y + 12f, 62f, 18f);
                RoundedBox(badge, new Color(0f, 0f, 0f, 0.28f), 9f);
                GUI.Label(badge, version, Badge);
            }
        }

        /// <summary>
        /// Same banner, with a clickable version badge for the update checker. The badge widens to fit
        /// (e.g. "v1.0.0 → v1.1.0") and turns amber when <paramref name="highlight"/> is set.
        /// Returns true when the badge is clicked.
        /// </summary>
        public static bool Header(string title, string subtitle, string version, string tooltip, bool highlight)
        {
            Rect rect = GUILayoutUtility.GetRect(0, int.MaxValue, 62f, 62f);
            rect = Inset(rect, 8f, 8f, 8f, 2f);

            GradientBox(rect, AccentA, AccentB, 10f);
            RoundedBox(new Rect(rect.x, rect.y + rect.height * 0.55f, rect.width, rect.height * 0.45f),
                new Color(0f, 0f, 0f, 0.13f), 10f);

            var content = new GUIContent(version, tooltip);
            float badgeWidth = Mathf.Max(62f, Badge.CalcSize(content).x + 18f);
            var badge = new Rect(rect.xMax - badgeWidth - 16f, rect.y + 12f, badgeWidth, 18f);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 9f, badge.x - rect.x - 24f, 24f), title, Title);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 32f, rect.width - 32f, 24f), subtitle, Subtitle);

            bool hover = badge.Contains(Event.current.mousePosition);
            Color fill = highlight ? Warn : new Color(0f, 0f, 0f, hover ? 0.40f : 0.28f);
            RoundedBox(badge, fill, 9f);
            var style = new GUIStyle(Badge);
            if (highlight) style.normal.textColor = new Color(0.12f, 0.08f, 0.02f);
            GUI.Label(badge, content, style);

            EditorGUIUtility.AddCursorRect(badge, MouseCursor.Link);
            return GUI.Button(badge, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        /// <summary>Opens a card panel. Always pair with <see cref="EndCard"/>.</summary>
        public static Rect BeginCard(string heading = null)
        {
            GUILayout.Space(6f);
            Rect rect = EditorGUILayout.BeginVertical();

            // The panel is painted behind the content, so it has to be drawn from the
            // rect the layout system reports - hence the repaint-only guard inside RoundedBox.
            RoundedBox(new Rect(rect.x + 6f, rect.y - 4f, rect.width - 12f, rect.height + 10f),
                Card, 8f, 1f, Line);

            GUILayout.Space(4f);

            if (!string.IsNullOrEmpty(heading))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(14f);

                    Rect dot = GUILayoutUtility.GetRect(4f, 4f, 13f, 13f, GUILayout.Width(4f));
                    RoundedBox(new Rect(dot.x, dot.y + 3f, 3f, 11f), AccentB, 1.5f);

                    GUILayout.Space(7f);
                    GUILayout.Label(heading, Section);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(2f);
            }

            EditorGUILayout.BeginVertical();
            return rect;
        }

        public static void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Indented content region, so fields do not sit flush against the card edge.</summary>
        public static void BeginCardBody()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            EditorGUILayout.BeginVertical();
        }

        public static void EndCardBody()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(14f);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Full-width gradient action button. Returns true on click.</summary>
        public static bool PrimaryButton(string label, float height = 40f, bool enabled = true)
        {
            Rect rect = GUILayoutUtility.GetRect(0, int.MaxValue, height, height);
            rect = Inset(rect, 14f, 14f, 2f, 2f);

            bool hover = rect.Contains(Event.current.mousePosition);

            if (enabled)
            {
                GradientBox(rect, AccentA, AccentB, 8f);
                if (hover)
                    RoundedBox(rect, new Color(1f, 1f, 1f, 0.10f), 8f);
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
            else
            {
                RoundedBox(rect, new Color(0.22f, 0.22f, 0.26f), 8f);
            }

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = enabled ? Color.white : new Color(1f, 1f, 1f, 0.35f) }
            };
            GUI.Label(rect, label, style);

            return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Quieter outlined button for secondary actions.</summary>
        public static bool GhostButton(string label, float width = 0f, float height = 24f)
        {
            Rect rect = width > 0f
                ? GUILayoutUtility.GetRect(width, height, GUILayout.Width(width))
                : GUILayoutUtility.GetRect(0, int.MaxValue, height, height);

            bool hover = rect.Contains(Event.current.mousePosition);

            RoundedBox(rect, hover ? CardHover : new Color(1f, 1f, 1f, 0.04f), 6f, 1f, Line);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = hover ? Color.white : TextPrimary }
            };
            GUI.Label(rect, label, style);

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Pill-shaped on/off toggle with a sliding knob.</summary>
        public static bool Toggle(string label, bool value, string tooltip = null)
        {
            Rect row = GUILayoutUtility.GetRect(0, int.MaxValue, 22f, 22f);

            var pill = new Rect(row.x, row.y + 3f, 32f, 16f);
            RoundedBox(pill, value ? AccentA : new Color(1f, 1f, 1f, 0.12f), 8f);

            var knob = new Rect(value ? pill.xMax - 14f : pill.x + 2f, pill.y + 2f, 12f, 12f);
            RoundedBox(knob, Color.white, 6f);

            var labelRect = new Rect(row.x + 40f, row.y, row.width - 40f, row.height);
            GUI.Label(labelRect, new GUIContent(label, tooltip), Body);

            EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                return !value;

            return value;
        }

        /// <summary>Coloured status strip, used for validation messages.</summary>
        public static void Notice(string message, Color color)
        {
            var content = new GUIContent(message);
            float height = Mathf.Max(24f, Body.CalcHeight(content, EditorGUIUtility.currentViewWidth - 80f) + 10f);

            Rect rect = GUILayoutUtility.GetRect(0, int.MaxValue, height, height);

            RoundedBox(rect, new Color(color.r, color.g, color.b, 0.10f), 6f);
            RoundedBox(new Rect(rect.x, rect.y, 3f, rect.height), color, 1.5f);

            GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 18f, rect.height - 8f), content, Body);
        }

        public static void Separator()
        {
            Rect rect = GUILayoutUtility.GetRect(0, int.MaxValue, 1f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y, rect.width - 8f, 1f), Line);
        }

        // ------------------------------------------------------------------
        // Social bar
        // ------------------------------------------------------------------

        /// <summary>
        /// NekoSuneVR social links. Each opens in the system browser via Application.OpenURL and
        /// shows a link cursor, the same pattern Doppel's shader GUI uses for its URL row.
        /// </summary>
        public static void SocialBar()
        {
            GUILayout.Space(8f);

            Rect row = GUILayoutUtility.GetRect(0, int.MaxValue, 34f, 34f);
            row = Inset(row, 14f, 14f, 0f, 0f);

            var label = new Rect(row.x, row.y, 62f, row.height);
            GUI.Label(label, "NekoSuneVR", new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextMuted }
            });

            float x = label.xMax + 6f;
            float width = Mathf.Min(96f, (row.width - 68f) / 3f - 6f);

            x = SocialButton(new Rect(x, row.y + 4f, width, 26f), "X", BrandX, UrlX) + 6f;
            x = SocialButton(new Rect(x, row.y + 4f, width, 26f), "Twitch", BrandTwitch, UrlTwitch) + 6f;
            SocialButton(new Rect(x, row.y + 4f, width, 26f), "YouTube", BrandYouTube, UrlYouTube);
        }

        /// <summary>Draws one link chip and returns its right edge, so callers can lay them out in a row.</summary>
        private static float SocialButton(Rect rect, string label, Color brand, string url)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            RoundedBox(rect, hover ? Lighten(brand, 0.18f) : brand, 6f, 1f, new Color(1f, 1f, 1f, 0.12f));

            GUI.Label(rect, label, new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (GUI.Button(rect, new GUIContent(string.Empty, url), GUIStyle.none))
            {
                Application.OpenURL(url);
                Debug.Log("[NekoSuneVR] Opened " + url);
            }

            return rect.xMax;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        public static Color Lighten(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        public static Rect Inset(Rect rect, float left, float right, float top, float bottom)
        {
            return new Rect(
                rect.x + left,
                rect.y + top,
                Mathf.Max(0f, rect.width - left - right),
                Mathf.Max(0f, rect.height - top - bottom));
        }

        /// <summary>Paints the window background. Call first in OnGUI.</summary>
        public static void WindowBackground(EditorWindow window)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(0f, 0f, window.position.width, window.position.height), Background);
        }

        /// <summary>A labelled field row that matches the card styling.</summary>
        public static void FieldLabel(string label, string tooltip = null)
        {
            GUILayout.Label(new GUIContent(label, tooltip), Muted);
        }
    }
}
#endif

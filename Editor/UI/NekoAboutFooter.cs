using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoUnityEssentials
{
    /// <summary>
    /// Footer "about" strip: NekoSuneVR badge, name, tagline and clickable social links with brand icons.
    /// Same layout as the Motion Converter footer. Icons are drawn procedurally (no image assets).
    /// To add or change a link, edit <see cref="Links"/>.
    /// </summary>
    internal static class NekoAboutFooter
    {
        public const string Author = "NekoSuneVR";

        private static bool Pro => EditorGUIUtility.isProSkin;
        private static Color HeaderBg => Pro ? new Color(0.13f, 0.14f, 0.17f) : new Color(0.80f, 0.82f, 0.86f);
        private static Color Muted => Pro ? new Color(0.62f, 0.64f, 0.68f) : new Color(0.35f, 0.36f, 0.40f);
        private static Color Border => Pro ? new Color(0.11f, 0.11f, 0.12f) : new Color(0.68f, 0.68f, 0.70f);

        /// <summary>1px separator + footer strip, pinned below the window's scroll view.</summary>
        public static void DrawWithSeparator()
        {
            var line = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, Border);
            Draw();
        }

        private enum Glyph { X, Twitch, YouTube, Patreon, KoFi, Bluesky, Discord, Link }

        private sealed class SocialLink
        {
            public readonly string Name, Url;
            public readonly Color Brand;
            public readonly Glyph Glyph;

            public SocialLink(string name, string url, Color brand, Glyph glyph)
            {
                Name = name; Url = url; Brand = brand; Glyph = glyph;
            }
        }

        private static readonly SocialLink[] Links =
        {
            new SocialLink("Twitch",  "https://twitch.tv/NekoSuneVR",      new Color(0.569f, 0.275f, 1.000f), Glyph.Twitch),
            new SocialLink("X",       "https://x.com/NekoSuneVR",          new Color(0.090f, 0.090f, 0.100f), Glyph.X),
            new SocialLink("YouTube", "https://youtube.com/@NekoSuneVR",   new Color(0.900f, 0.000f, 0.000f), Glyph.YouTube),
            new SocialLink("Bluesky", "https://bsky.app/profile/cc.nekosunevr.co.uk", new Color(0.067f, 0.522f, 0.996f), Glyph.Bluesky),
            new SocialLink("Discord", "https://discord.gg/vJTByM36yF",     new Color(0.345f, 0.396f, 0.949f), Glyph.Discord),
            new SocialLink("Patreon", "https://patreon.com/NekoSuneVR",    new Color(0.976f, 0.408f, 0.329f), Glyph.Patreon),
            new SocialLink("Ko-fi",   "https://ko-fi.com/nekosunevr",      new Color(0.161f, 0.671f, 0.878f), Glyph.KoFi),
        };

        private static readonly Color BadgeA = new Color(0.659f, 0.333f, 0.969f); // violet
        private static readonly Color BadgeB = new Color(0.925f, 0.282f, 0.600f); // pink

        private const float RowHeight = 40f;
        private const float ChipHeight = 24f;
        private const float ChipGap = 5f;
        private const float IconSize = 14f;

        private static readonly Dictionary<Glyph, Texture2D> IconCache = new Dictionary<Glyph, Texture2D>();
        private static Texture2D badgeTex;
        private static GUIStyle nameStyle, taglineStyle, chipLabel, badgeLabel;

        public static void Draw()
        {
            EnsureStyles();

            var row = GUILayoutUtility.GetRect(1, RowHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(row, HeaderBg);
            row = new Rect(row.x + 10, row.y, row.width - 20, row.height);

            // Chips: with labels if they fit, icon-only otherwise.
            const float minTextBlock = 120f;
            float labelled = ChipsWidth(true);
            bool showLabels = row.width - 38 - minTextBlock >= labelled;
            float chipsW = showLabels ? labelled : ChipsWidth(false);

            float x = row.xMax - chipsW;
            float y = row.y + (row.height - ChipHeight) * 0.5f;
            foreach (var link in Links)
                x = Chip(new Rect(x, y, ChipWidth(link, showLabels), ChipHeight), link, showLabels) + ChipGap;

            // Badge + name + tagline.
            var badge = new Rect(row.x, row.y + (row.height - 28) * 0.5f, 28, 28);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(badge, BadgeTexture(), ScaleMode.StretchToFill, true, 0, Color.white, Vector4.zero, Vector4.one * 14f);
            GUI.Label(badge, "NS", badgeLabel);

            float textX = badge.xMax + 8;
            float textW = Mathf.Max(0, row.xMax - chipsW - 8 - textX);
            var tagline = "Creator of this tool.";
            GUI.Label(new Rect(textX, row.y + 4, textW, 18), Author, nameStyle);
            GUI.Label(new Rect(textX, row.y + 20, textW, 16), new GUIContent(tagline, tagline), taglineStyle);
        }

        private static float ChipsWidth(bool labels)
        {
            float w = 0;
            foreach (var link in Links) w += ChipWidth(link, labels) + ChipGap;
            return w - ChipGap;
        }

        private static float ChipWidth(SocialLink link, bool labels) =>
            labels ? 8 + IconSize + 5 + chipLabel.CalcSize(new GUIContent(link.Name)).x + 9 : ChipHeight;

        /// <summary>One brand-coloured link chip. Returns its right edge.</summary>
        private static float Chip(Rect rect, SocialLink link, bool label)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                var fill = hover ? Color.Lerp(link.Brand, Color.white, 0.18f) : link.Brand;
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, fill, Vector4.zero, Vector4.one * 6f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0,
                    new Color(1, 1, 1, hover ? 0.35f : 0.14f), Vector4.one, Vector4.one * 6f);

                float iconX = label ? rect.x + 8 : rect.x + (rect.width - IconSize) * 0.5f;
                var iconRect = new Rect(iconX, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize);
                GUI.DrawTexture(iconRect, Icon(link.Glyph), ScaleMode.ScaleToFit, true);

                if (label)
                    GUI.Label(new Rect(iconRect.xMax + 5, rect.y, rect.xMax - iconRect.xMax - 5, rect.height), link.Name, chipLabel);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            var tip = new GUIContent(string.Empty, ("Open " + link.Name) + "\n" + link.Url);
            if (GUI.Button(rect, tip, GUIStyle.none)) Application.OpenURL(link.Url);

            return rect.xMax;
        }

        private static void EnsureStyles()
        {
            if (nameStyle != null) return;

            nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, clipping = TextClipping.Clip };
            taglineStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            taglineStyle.normal.textColor = Muted;
            chipLabel = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };
            chipLabel.normal.textColor = Color.white;
            badgeLabel = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            badgeLabel.normal.textColor = Color.white;
        }

        // ------------------------------------------------------------------
        // Procedural textures
        // ------------------------------------------------------------------

        private static Texture2D BadgeTexture()
        {
            if (badgeTex != null) return badgeTex;
            const int n = 64;
            badgeTex = NewTexture(n);
            for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
                badgeTex.SetPixel(px, py, Color.Lerp(BadgeA, BadgeB, (px + (n - 1 - py)) / (2f * (n - 1))));
            badgeTex.Apply();
            return badgeTex;
        }

        /// <summary>White glyph on transparent, 4x4 supersampled for smooth edges.</summary>
        private static Texture2D Icon(Glyph glyph)
        {
            if (IconCache.TryGetValue(glyph, out var tex) && tex != null) return tex;

            const int n = 64, ss = 4;
            Func<float, float, bool> inside = Shape(glyph);
            tex = NewTexture(n);
            for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float u = (px + (sx + 0.5f) / ss) / n;
                    float v = (py + (sy + 0.5f) / ss) / n;
                    if (inside(u, 1f - v)) hits++; // shapes use y-down coordinates
                }
                tex.SetPixel(px, py, new Color(1, 1, 1, hits / (float)(ss * ss)));
            }
            tex.Apply();
            IconCache[glyph] = tex;
            return tex;
        }

        private static Texture2D NewTexture(int n) =>
            new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

        /// <summary>Glyph outlines in a 0..1 box, y pointing down.</summary>
        private static Func<float, float, bool> Shape(Glyph glyph)
        {
            switch (glyph)
            {
                case Glyph.X:
                    return (x, y) =>
                        Segment(x, y, 0.20f, 0.14f, 0.80f, 0.86f) < 0.075f ||
                        Segment(x, y, 0.80f, 0.14f, 0.20f, 0.86f) < 0.035f;

                case Glyph.YouTube:
                    return (x, y) => Triangle(x, y, 0.30f, 0.18f, 0.30f, 0.82f, 0.86f, 0.50f);

                case Glyph.Patreon:
                    return (x, y) =>
                        Circle(x, y, 0.60f, 0.40f, 0.27f) ||
                        Box(x, y, 0.12f, 0.12f, 0.27f, 0.88f);

                case Glyph.Twitch:
                    return (x, y) =>
                    {
                        // Speech bubble with a cut bottom-right corner and a tail, two "eyes" inside.
                        bool outer = Box(x, y, 0.10f, 0.04f, 0.90f, 0.74f) && (x + y) < 1.52f;
                        bool tail = y >= 0.74f && y <= 0.96f && x >= 0.30f && x <= 0.52f - (y - 0.74f);
                        bool inner = Box(x, y, 0.22f, 0.15f, 0.78f, 0.63f) && (x + y) < 1.30f;
                        bool eyes = Box(x, y, 0.40f, 0.26f, 0.49f, 0.50f) || Box(x, y, 0.59f, 0.26f, 0.68f, 0.50f);
                        return (outer || tail) && !inner || eyes;
                    };

                case Glyph.KoFi:
                    return (x, y) =>
                    {
                        // Coffee cup with a handle, heart cut out of the cup.
                        bool cup = Box(x, y, 0.08f, 0.22f, 0.70f, 0.62f) || Ellipse(x, y, 0.39f, 0.62f, 0.31f, 0.20f) && y >= 0.62f;
                        float hx = x - 0.70f, hy = y - 0.44f;
                        float hd = Mathf.Sqrt(hx * hx + hy * hy);
                        bool handle = x > 0.66f && hd < 0.21f && hd > 0.10f;
                        bool heart = Circle(x, y, 0.33f, 0.40f, 0.075f) || Circle(x, y, 0.46f, 0.40f, 0.075f) ||
                                     Triangle(x, y, 0.26f, 0.43f, 0.53f, 0.43f, 0.395f, 0.60f);
                        return (cup || handle) && !heart;
                    };

                case Glyph.Bluesky:
                    return (x, y) =>
                        Ellipse(x, y, 0.29f, 0.37f, 0.25f, 0.22f) || Ellipse(x, y, 0.71f, 0.37f, 0.25f, 0.22f) ||
                        Ellipse(x, y, 0.36f, 0.66f, 0.16f, 0.15f) || Ellipse(x, y, 0.64f, 0.66f, 0.16f, 0.15f) ||
                        Box(x, y, 0.44f, 0.42f, 0.56f, 0.70f);

                case Glyph.Discord:
                    return (x, y) =>
                    {
                        // Controller-shaped face with two eyes.
                        bool body = Ellipse(x, y, 0.50f, 0.55f, 0.44f, 0.27f) || Box(x, y, 0.16f, 0.30f, 0.84f, 0.60f) ||
                                    Ellipse(x, y, 0.30f, 0.30f, 0.16f, 0.12f) || Ellipse(x, y, 0.70f, 0.30f, 0.16f, 0.12f);
                        bool eyes = Ellipse(x, y, 0.36f, 0.53f, 0.075f, 0.09f) || Ellipse(x, y, 0.64f, 0.53f, 0.075f, 0.09f);
                        return body && !eyes;
                    };

                default: // Link: a ring with a dot
                    return (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
                        return (d < 0.42f && d > 0.30f) || d < 0.12f;
                    };
            }
        }

        private static bool Box(float x, float y, float x0, float y0, float x1, float y1) =>
            x >= x0 && x <= x1 && y >= y0 && y <= y1;

        private static bool Circle(float x, float y, float cx, float cy, float r) =>
            (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

        private static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Distance from (x,y) to segment a-b.</summary>
        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}

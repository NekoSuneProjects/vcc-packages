using UnityEditor;
using UnityEngine;

namespace NekoUnityEssentials
{
    [InitializeOnLoad]
    public static class NekoHierarchyProjectRenderer
    {
        static NekoHierarchyProjectRenderer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += DrawHierarchy;
            EditorApplication.projectWindowItemOnGUI += DrawProject;
        }

        private static NekoStyleRule FindHierarchyRule(Transform t, NekoEssentialsSettings settings)
        {
            var direct = NekoEssentialsSettings.FindRule(settings.hierarchyRules, t.name);
            if (direct != null) return direct;
            for (var parent = t.parent; parent != null; parent = parent.parent)
            {
                var inherited = NekoEssentialsSettings.FindRule(settings.hierarchyRules, parent.name);
                if (inherited != null && inherited.applyToChildren) return inherited;
            }
            return null;
        }

        private static void DrawHierarchy(int id, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var settings = NekoEssentialsSettings.Instance;
            if (!settings || !settings.hierarchyEnabled) return;
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (!go) return;
            var rule = FindHierarchyRule(go.transform, settings);
            if (rule == null) return;
            DrawDecorations(rect, rule, settings.showObjectCounts ? go.transform.childCount.ToString() : null);
        }

        private static void DrawProject(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var settings = NekoEssentialsSettings.Instance;
            if (!settings || !settings.foldersEnabled) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;
            var name = System.IO.Path.GetFileName(path);
            var rule = NekoEssentialsSettings.FindRule(settings.folderRules, name);
            if (rule == null) return;
            // Folder view supports both one-column and grid layouts.
            DrawDecorations(rect, rule, null);
        }

        internal static void DrawDecorations(Rect rect, NekoStyleRule rule, string count)
        {
            if (rect.height > 24) // Project grid item: don't obstruct large folder thumbnails.
            {
                var small = new Rect(rect.x, rect.y + rect.height - 19, rect.width, 19);
                EditorGUI.DrawRect(small, rule.background);
                return;
            }
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), rule.background);
            float right = rect.xMax - 5;
            if (!string.IsNullOrEmpty(count))
            {
                var width = 23f;
                GUI.Label(new Rect(right - width, rect.y, width, rect.height), count, EditorStyles.miniLabel);
                right -= width + 2;
            }
            if (!string.IsNullOrEmpty(rule.badge))
            {
                var badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                badgeStyle.normal.textColor = rule.foreground;
                float width = Mathf.Clamp(rule.badge.Length * 7 + 8, 22, 70);
                EditorGUI.DrawRect(new Rect(right - width, rect.y + 1, width, rect.height - 2), new Color(0, 0, 0, .35f));
                GUI.Label(new Rect(right - width, rect.y, width, rect.height), rule.badge, badgeStyle);
                right -= width + 4;
            }
            if (rule.icon)
                GUI.DrawTexture(new Rect(right - 16, rect.y + 1, 16, 16), rule.icon, ScaleMode.ScaleToFit);
            // Avoid painting the text over the native Hierarchy labels to preserve selection,
            // indentation, context menus, prefab markers and Unity's scene controls.
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using S = NekoUnityEssentials.EssentialsStyles;
using G = NekoUnityEssentials.EssentialsGUI;

namespace NekoUnityEssentials
{
    /// <summary>Organizer tab: styling switches, colour/badge/icon rules with a live preview, and quick actions.</summary>
    [Serializable]
    internal class NekoOrganizerPanel
    {
        [SerializeField] private bool hierarchyOpen = true, folderOpen = true;
        [SerializeField] private int expandedHierarchy = -1, expandedFolder = -1;

        public void OnGUI(EditorWindow host)
        {
            var settings = NekoEssentialsSettings.Instance;
            if (!settings)
            {
                DrawGetStarted();
                return;
            }

            DrawSwitches(settings);
            DrawRules(settings, "UnityEditor.SceneHierarchyWindow", "Hierarchy rules",
                "Colour objects in the Hierarchy whose name matches. Name groups like  --- Enemies ---  to match a heading rule.",
                settings.hierarchyRules, true, ref hierarchyOpen, ref expandedHierarchy);
            DrawRules(settings, "Folder Icon", "Folder rules",
                "Colour folders in the Project window whose name matches. Only the editor view changes, never the folders on disk.",
                settings.folderRules, false, ref folderOpen, ref expandedFolder);
            DrawActions(settings);
        }

        private static void DrawGetStarted()
        {
            G.BeginCard("Folder Icon", "Get started");
            G.Hint("Unity Essentials colours your Hierarchy and Project window by name, so headings, cameras, UI and folders stand out at a glance.");
            EditorGUILayout.Space(4);
            G.Hint("Settings are saved per project in " + NekoEssentialsSettings.AssetPath + ".");
            EditorGUILayout.Space(8);
            if (G.Action("Toolbar Plus", "Create Default Settings", primary: true)) NekoEssentialsWindow.CreateSettings();
            G.EndCard();
        }

        private static void DrawSwitches(NekoEssentialsSettings settings)
        {
            G.BeginCard("SettingsIcon", "Styling");
            EditorGUI.BeginChangeCheck();
            bool hierarchy = EditorGUILayout.ToggleLeft(new GUIContent(" Style the Hierarchy", "Colour and badge matching objects in the Hierarchy window."), settings.hierarchyEnabled);
            bool folders = EditorGUILayout.ToggleLeft(new GUIContent(" Style Project folders", "Colour and badge matching folders in the Project window."), settings.foldersEnabled);
            bool counts = EditorGUILayout.ToggleLeft(new GUIContent(" Show child counts", "Show how many children a styled Hierarchy object has."), settings.showObjectCounts);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Change Neko Essentials styling");
                settings.hierarchyEnabled = hierarchy;
                settings.foldersEnabled = folders;
                settings.showObjectCounts = counts;
                Changed(settings);
            }
            G.EndCard();
        }

        private static void DrawRules(NekoEssentialsSettings settings, string icon, string title, string hint,
            List<NekoStyleRule> rules, bool hierarchy, ref bool open, ref int expanded)
        {
            G.BeginCard(null, null);
            using (new EditorGUILayout.HorizontalScope())
            {
                open = EditorGUILayout.Foldout(open, new GUIContent(" " + title, EditorGUIUtility.IconContent(icon).image), true, FoldoutTitle);
                GUILayout.FlexibleSpace();
                G.Chip(rules.Count + (rules.Count == 1 ? " rule" : " rules"));
            }
            if (!open)
            {
                G.EndCard();
                return;
            }
            S.Line();
            EditorGUILayout.Space(4);
            G.Hint(hint);
            EditorGUILayout.Space(4);

            int remove = -1, moveFrom = -1, moveTo = -1;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null) continue;
                EditorGUILayout.BeginVertical(S.RuleCard);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isOpen = expanded == i;
                    var arrow = EditorGUIUtility.IconContent(isOpen ? "IN foldout on" : "IN foldout");
                    if (GUILayout.Button(arrow, EditorStyles.label, GUILayout.Width(14), GUILayout.Height(18))) expanded = isOpen ? -1 : i;

                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(16));
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(settings, "Toggle rule"); rule.enabled = enabled; Changed(settings); }

                    DrawPreview(GUILayoutUtility.GetRect(60, 18, GUILayout.ExpandWidth(true), GUILayout.Height(18)), rule, hierarchy && settings.showObjectCounts, hierarchy, ref expanded, i);

                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button(new GUIContent("▲", "Move up"), EditorStyles.miniButtonLeft, GUILayout.Width(22))) { moveFrom = i; moveTo = i - 1; }
                    using (new EditorGUI.DisabledScope(i == rules.Count - 1))
                        if (GUILayout.Button(new GUIContent("▼", "Move down"), EditorStyles.miniButtonMid, GUILayout.Width(22))) { moveFrom = i; moveTo = i + 1; }
                    if (GUILayout.Button(new GUIContent("✕", "Remove rule"), EditorStyles.miniButtonRight, GUILayout.Width(22))) remove = i;
                }

                if (expanded == i) DrawRuleFields(settings, rule, hierarchy);
                EditorGUILayout.EndVertical();
            }

            if (rules.Count == 0) G.Hint("No rules yet.");

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(S.Icon("Toolbar Plus", " Add rule"), GUILayout.Height(22), GUILayout.Width(110)))
                {
                    Undo.RecordObject(settings, "Add rule");
                    rules.Add(new NekoStyleRule { match = hierarchy ? "--- Group ---" : "Folder", badge = "", background = new Color(S.Accent.r, S.Accent.g, S.Accent.b, 0.55f) });
                    expanded = rules.Count - 1;
                    Changed(settings);
                }
            }

            if (remove >= 0)
            {
                Undo.RecordObject(settings, "Remove rule");
                rules.RemoveAt(remove);
                if (expanded == remove) expanded = -1;
                else if (expanded > remove) expanded--;
                Changed(settings);
                GUIUtility.ExitGUI();
            }
            else if (moveFrom >= 0)
            {
                Undo.RecordObject(settings, "Reorder rules");
                var r = rules[moveFrom];
                rules[moveFrom] = rules[moveTo];
                rules[moveTo] = r;
                if (expanded == moveFrom) expanded = moveTo;
                else if (expanded == moveTo) expanded = moveFrom;
                Changed(settings);
            }
            G.EndCard();
        }

        /// <summary>Draws the rule the way it will look in the Hierarchy/Project row. Click to expand.</summary>
        private static void DrawPreview(Rect r, NekoStyleRule rule, bool showCount, bool hierarchy, ref int expanded, int index)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, S.RowBg);
                var icon = EditorGUIUtility.IconContent(hierarchy ? "GameObject Icon" : "Folder Icon").image;
                GUI.color = rule.enabled ? Color.white : new Color(1, 1, 1, 0.4f);
                NekoHierarchyProjectRenderer.DrawDecorations(r, rule, showCount ? "3" : null);
                if (icon != null) GUI.DrawTexture(new Rect(r.x + 3, r.y + 1, 16, 16), icon, ScaleMode.ScaleToFit);
                var label = new GUIStyle(EditorStyles.label) { fontStyle = rule.bold ? FontStyle.Bold : FontStyle.Normal, clipping = TextClipping.Clip };
                label.normal.textColor = S.Text;
                string sample = string.IsNullOrEmpty(rule.match) ? "(no match text)" : rule.match;
                GUI.Label(new Rect(r.x + 21, r.y, r.width - 100, r.height), sample, label);
                GUI.color = Color.white;
            }
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                expanded = expanded == index ? -1 : index;
                Event.current.Use();
            }
        }

        private static void DrawRuleFields(NekoEssentialsSettings settings, NekoStyleRule rule, bool hierarchy)
        {
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            string match;
            NekoMatchMode mode;
            using (new EditorGUILayout.HorizontalScope())
            {
                match = EditorGUILayout.TextField(new GUIContent("Name", "Text to look for in the object or folder name (case is ignored)."), rule.match);
                mode = (NekoMatchMode)EditorGUILayout.EnumPopup(rule.matchMode, GUILayout.Width(90));
            }
            var background = EditorGUILayout.ColorField(new GUIContent("Row colour"), rule.background);
            var foreground = EditorGUILayout.ColorField(new GUIContent("Badge text colour"), rule.foreground);
            string badge = EditorGUILayout.TextField(new GUIContent("Badge", "Short tag shown on the right, e.g. NPC, CAM, UI. Leave empty for none."), rule.badge);
            var icon = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Icon", "Optional small image shown next to the badge."), rule.icon, typeof(Texture2D), false, GUILayout.Height(16));
            int priority = EditorGUILayout.IntField(new GUIContent("Priority", "When several rules match, the highest priority wins."), rule.priority);
            bool bold = EditorGUILayout.Toggle(new GUIContent("Bold preview"), rule.bold);
            bool children = rule.applyToChildren;
            if (hierarchy)
                children = EditorGUILayout.Toggle(new GUIContent("Colour children too", "Objects under a matching parent get this style unless they match a rule themselves."), rule.applyToChildren);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Edit rule");
                rule.match = match;
                rule.matchMode = mode;
                rule.background = background;
                rule.foreground = foreground;
                rule.badge = badge;
                rule.icon = icon;
                rule.priority = priority;
                rule.bold = bold;
                rule.applyToChildren = children;
                Changed(settings);
            }
        }

        private static void DrawActions(NekoEssentialsSettings settings)
        {
            G.BeginCard("d_Toolbar Plus More", "Quick actions");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
                    if (GUILayout.Button(new GUIContent(" Group selected", EditorGUIUtility.IconContent("GameObject Icon").image, "Put the selected Hierarchy objects under a new  --- New Group ---  object."), GUILayout.Height(24)))
                        NekoOrganizationTools.GroupSelected();
                if (GUILayout.Button(new GUIContent(" Standard folders", EditorGUIUtility.IconContent("Folder Icon").image, "Create Animations, Materials, Prefabs, Scripts, Textures, Audio, Models and Editor in the selected Project folder."), GUILayout.Height(24)))
                    NekoOrganizationTools.CreateFolders();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(" Repaint windows", EditorGUIUtility.IconContent("Refresh").image), GUILayout.Height(22))) RepaintViews();
                if (GUILayout.Button(new GUIContent(" Select settings asset", EditorGUIUtility.IconContent("ScriptableObject Icon").image), GUILayout.Height(22)))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
            EditorGUILayout.Space(2);
            G.Hint("Also in the right-click menus: GameObject → Neko Essentials → Group Selected Objects, and Assets → Neko Essentials → Create Standard Folders.");
            G.EndCard();
        }

        private static void Changed(NekoEssentialsSettings settings)
        {
            EditorUtility.SetDirty(settings);
            RepaintViews();
        }

        private static void RepaintViews()
        {
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        private static GUIStyle foldoutTitle;
        private static GUIStyle FoldoutTitle =>
            foldoutTitle ?? (foldoutTitle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12, fixedHeight = 20 });
    }
}

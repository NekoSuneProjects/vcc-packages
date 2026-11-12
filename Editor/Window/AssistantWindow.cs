using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// World UI Assistant: build VRChat world UI without knowing Unity UI. Pick or create a panel,
    /// add widgets from the palette, arrange them in the outline and edit them in plain English.
    /// Everything it makes is normal Unity UI; the scene is the saved state.
    /// </summary>
    internal partial class AssistantWindow : EditorWindow
    {
        private const string HeaderIcon = "Canvas Icon";
        private const string ThemePrefPrefix = "NekoSune.WorldUiAssistant.Theme.";
        private const string LastThemePref = "NekoSune.WorldUiAssistant.LastTheme";

        private static readonly (string label, Vector2 size)[] SizePresets =
        {
            ("Small sign  (60 × 40 cm)", new Vector2(600, 400)),
            ("Menu  (80 × 100 cm)", new Vector2(800, 1000)),
            ("Big board  (160 × 90 cm)", new Vector2(1600, 900)),
        };

        [SerializeField] private Canvas panel;
        [SerializeField] private Transform selected;
        [SerializeField] private bool showWizard;
        [SerializeField] private string newName = "Welcome Board";
        [SerializeField] private int newSize = 1;
        [SerializeField] private UiThemeId newTheme = UiThemeId.NekoDark;
        [SerializeField] private PanelPlacement newPlacement = PanelPlacement.InFrontOfSceneCamera;
        [SerializeField] private bool newWithTitle = true;
        [SerializeField] private bool panelOpen = true;
        private Vector2 scroll;
        private List<Canvas> panels = new List<Canvas>();
        private bool panelsDirty = true;

        [MenuItem("NekoSune/World/UI Assistant", false, 10)]
        public static void Open()
        {
            var w = GetWindow<AssistantWindow>();
            w.titleContent = new GUIContent("UI Assistant", EditorGUIUtility.IconContent(HeaderIcon).image);
            w.minSize = new Vector2(400f, 520f);
            w.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("UI Assistant", EditorGUIUtility.IconContent(HeaderIcon).image);
            newTheme = (UiThemeId)EditorPrefs.GetInt(LastThemePref, (int)newTheme);
            UpdateChecker.Changed += Repaint;
            Undo.undoRedoPerformed += OnUndoRedo;
            Selection.selectionChanged += SelectionChanged;
            panelsDirty = true;
        }

        private void OnDisable()
        {
            UpdateChecker.Changed -= Repaint;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Selection.selectionChanged -= SelectionChanged;
        }

        private void OnUndoRedo()
        {
            panelsDirty = true;
            Repaint();
        }

        private void OnHierarchyChange()
        {
            panelsDirty = true;
            Repaint();
        }

        /// <summary>Selecting something inside a panel in the Hierarchy selects it here too.</summary>
        private void SelectionChanged()
        {
            var t = Selection.activeTransform;
            if (!t) { Repaint(); return; }
            var canvas = t.GetComponentInParent<Canvas>(true);
            if (canvas) canvas = canvas.rootCanvas;
            if (canvas && canvas.renderMode == RenderMode.WorldSpace)
            {
                panel = canvas;
                showWizard = false;
                selected = OutlineItemFor(t);
            }
            Repaint();
        }

        // ------------------------------------------------------------------ state helpers

        private UiThemeId ThemeIdOf(Canvas c)
        {
            if (!c) return newTheme;
            return (UiThemeId)EditorPrefs.GetInt(ThemePrefPrefix + GlobalObjectId.GetGlobalObjectIdSlow(c), (int)UiThemeId.NekoDark);
        }

        private void SetThemeId(Canvas c, UiThemeId id)
        {
            EditorPrefs.SetInt(ThemePrefPrefix + GlobalObjectId.GetGlobalObjectIdSlow(c), (int)id);
            EditorPrefs.SetInt(LastThemePref, (int)id);
        }

        private UiTheme CurrentTheme => UiTheme.Get(ThemeIdOf(panel));

        private void RefreshPanels()
        {
            if (!panelsDirty) return;
            panelsDirty = false;
            panels = Resources.FindObjectsOfTypeAll<Canvas>()
                .Where(c => c && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid() && c.hideFlags == HideFlags.None
                            && c.isRootCanvas && c.renderMode == RenderMode.WorldSpace)
                .OrderBy(c => c.name)
                .ToList();
        }

        private void Select(Transform t)
        {
            selected = t;
            if (t) Selection.activeGameObject = t.gameObject;
            GUI.FocusControl(null);
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            S.Ensure();
            EditorGUIUtility.SetIconSize(new Vector2(16f, 16f));
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.36f, 120f, 200f);
            RefreshPanels();
            if (!panel) { panel = null; selected = null; }
            if (selected && (!panel || !selected.IsChildOf(panel.transform))) selected = null;

            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            DrawUpdateBanner();
            DrawEnvironment();
            DrawPanelPicker();

            if (showWizard || !panel)
            {
                DrawWizard();
            }
            else
            {
                DrawPanelCard();
                DrawPalette();
                DrawOutline();
                DrawItemCard();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
            EditorGUIUtility.SetIconSize(Vector2.zero);
            NekoAboutFooter.DrawWithSeparator();
        }

        private void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(1, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), S.Accent);
            var icon = EditorGUIUtility.IconContent(HeaderIcon).image;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 14, rect.y + 13, 30, 30), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(rect.x + 54, rect.y + 9, rect.width - 160, 24), "World UI Assistant", S.Title);
            GUI.Label(new Rect(rect.x + 55, rect.y + 32, rect.width - 70, 16), "Build VRChat world UI step by step — and learn how it works", S.Subtitle);
            DrawVersionChip(new Rect(rect.x, rect.y + 11, rect.width - 12, 18));
        }

        private static void DrawVersionChip(Rect area)
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip = "Click to check for updates";
            bool update = false;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking: text = current + "  ···"; tip = "Checking for updates…"; break;
                case UpdateChecker.State.UpdateAvailable:
                    update = true;
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Version " + UpdateChecker.LatestVersion + " is available.\nClick to check again.";
                    break;
                case UpdateChecker.State.UpToDate: tip = "Up to date.\nClick to check again."; break;
                case UpdateChecker.State.Failed: tip = "Couldn't check for updates. Click to try again."; break;
            }
            var content = new GUIContent(text, tip);
            var size = S.Chip.CalcSize(content);
            var r = new Rect(area.xMax - size.x, area.y, size.x, area.height);
            var style = S.Chip;
            if (update)
            {
                style = new GUIStyle(S.Chip) { fontStyle = FontStyle.Bold };
                style.normal.textColor = S.Warning;
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, content, style)) UpdateChecker.Check();
        }

        private static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;
            G.BeginCard("console.infoicon", "World UI Assistant v" + UpdateChecker.LatestVersion + " is available");
            EditorGUILayout.LabelField("You have v" + UpdateChecker.CurrentVersion + ". Update it in the VRChat Creator Companion.", EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes)) EditorGUILayout.LabelField(UpdateChecker.Notes, S.PathLabel);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = S.Warning;
                if (GUILayout.Button("Download update", GUILayout.Height(24))) UpdateChecker.OpenDownload();
                GUI.backgroundColor = old;
                if (GUILayout.Button("Later", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button("Skip this version", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
            G.EndCard();
        }

        /// <summary>Only shown when something in the project needs attention.</summary>
        private static void DrawEnvironment()
        {
            bool unityOk = Application.unityVersion.StartsWith("2022.3");
            bool sdkOk = UiFactory.UiShapeType != null;
            bool fontOk = UiFactory.HasTmpFont;
            if (unityOk && sdkOk && fontOk) return;

            G.BeginCard("console.warnicon", "Check your project");
            if (!unityOk)
                G.Report("Unity " + Application.unityVersion + " — VRChat worlds are built with Unity 2022.3. Things may still work, but test before uploading.", false);
            if (!sdkOk)
                G.Report("VRChat Worlds SDK not found. Panels are still created, but without VRCUiShape the VRChat laser can't click them. Add the Worlds SDK in the Creator Companion.", false);
            if (!fontOk)
            {
                G.Report("TextMeshPro's default font isn't imported yet. Text needs it.", false);
                if (GUILayout.Button(S.Icon("Import", " Import TMP Essentials"), GUILayout.Height(24)))
                    TMPro.TMP_PackageResourceImporterWindow.ShowPackageImporterWindow();
            }
            G.EndCard();
        }

        private void DrawPanelPicker()
        {
            G.BeginCard("Canvas Icon", "Your panels");
            if (panels.Count == 0)
            {
                G.Hint("No world UI panels in the open scene yet. Make your first one below.");
                showWizard = true;
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int current = panel ? panels.IndexOf(panel) : -1;
                    var names = panels.Select(c => c.name).ToArray();
                    int picked = EditorGUILayout.Popup(current, names);
                    if (picked != current && picked >= 0)
                    {
                        panel = panels[picked];
                        showWizard = false;
                        Select(panel.transform);
                        selected = null;
                    }
                    using (new EditorGUI.DisabledScope(showWizard))
                        if (GUILayout.Button(S.Icon("Toolbar Plus", " New panel"), GUILayout.Width(100)))
                            showWizard = true;
                }
                if (!panel && !showWizard) G.Hint("Pick a panel to edit, or make a new one.");
            }
            G.EndCard();
        }

        // ------------------------------------------------------------------ wizard

        private void DrawWizard()
        {
            G.BeginCard("Toolbar Plus", "New panel", "A panel is a flat board in your world that players point at with the VRChat laser.");

            newName = EditorGUILayout.TextField(new GUIContent("Name", "What it's called in the Hierarchy."), newName);

            G.Group("Size");
            for (int i = 0; i < SizePresets.Length; i++)
                if (GUILayout.Toggle(newSize == i, " " + SizePresets[i].label, EditorStyles.radioButton)) newSize = i;

            G.Group("Look");
            newTheme = DrawThemePicker(newTheme);

            G.Group("Where");
            newPlacement = (PanelPlacement)EditorGUILayout.Popup(new GUIContent("Place it"), (int)newPlacement, new[]
            {
                new GUIContent("In front of the Scene view camera"),
                new GUIContent("At the world origin (1.5 m up)"),
                new GUIContent("Inside the selected object"),
            });
            newWithTitle = EditorGUILayout.ToggleLeft(new GUIContent(" Start with a title and some text"), newWithTitle);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (G.Action("Toolbar Plus", "Create Panel", UiFactory.HasTmpFont || !newWithTitle, true)) CreatePanel();
                if (panel && GUILayout.Button("Cancel", S.BigButton, GUILayout.Width(80))) showWizard = false;
            }
            EditorGUILayout.Space(2);
            G.Hint("1 UI unit = 1 mm in the world, so a 800 × 1000 menu is 80 cm wide and 1 m tall.");
            G.EndCard();
        }

        private void CreatePanel()
        {
            var theme = UiTheme.Get(newTheme);
            var canvas = UiFactory.CreatePanel(newName, SizePresets[newSize].size, theme, newPlacement);
            SetThemeId(canvas, newTheme);
            if (newWithTitle)
            {
                UiFactory.CreateTitle(canvas.transform, -1, theme);
                var text = UiFactory.Create(WidgetKind.Text, canvas.transform, -1, theme);
                text.GetComponent<TMPro.TMP_Text>().text = "Welcome! Have fun and be kind to everyone here.";
            }
            panel = canvas;
            showWizard = false;
            panelsDirty = true;
            Select(canvas.transform);
            selected = null;
            UiFactory.Frame((RectTransform)canvas.transform);
        }

        /// <summary>Theme swatches in a grid; returns the clicked (or current) theme.</summary>
        private UiThemeId DrawThemePicker(UiThemeId current)
        {
            int cols = Mathf.Clamp(Mathf.FloorToInt((position.width - 40f) / 110f), 2, 6);
            var all = UiTheme.All;
            for (int i = 0; i < all.Length; i += cols)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + cols, all.Length); j++)
                    {
                        var r = GUILayoutUtility.GetRect(90, 52, GUILayout.ExpandWidth(true));
                        if (DrawSwatch(r, all[j], all[j] == current)) current = all[j];
                    }
                    for (int j = all.Length; j < i + cols; j++) GUILayoutUtility.GetRect(90, 52, GUILayout.ExpandWidth(true));
                }
            }
            return current;
        }

        private static bool DrawSwatch(Rect r, UiThemeId id, bool on)
        {
            r = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            var t = UiTheme.Get(id);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, on ? S.Accent : S.CardBorder);
                var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
                EditorGUI.DrawRect(inner, UiFactory.WithAlpha(t.background, 1f));
                EditorGUI.DrawRect(new Rect(inner.x + 8, inner.y + 8, inner.width * 0.45f, 6), t.text);
                EditorGUI.DrawRect(new Rect(inner.x + 8, inner.y + 18, inner.width - 16, 10), t.primary);
                EditorGUI.DrawRect(new Rect(inner.xMax - 18, inner.y + 6, 10, 10), t.accent);
                var label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerLeft };
                label.normal.textColor = t.mutedText;
                GUI.Label(new Rect(inner.x + 6, inner.y, inner.width - 8, inner.height - 1), UiTheme.DisplayName(id), label);
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ panel card

        private void DrawPanelCard()
        {
            var rt = (RectTransform)panel.transform;
            G.BeginCard(null, null);
            using (new EditorGUILayout.HorizontalScope())
            {
                panelOpen = EditorGUILayout.Foldout(panelOpen, new GUIContent(" " + panel.name, EditorGUIUtility.IconContent("Canvas Icon").image), true, G.FoldoutTitle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(" Frame", EditorGUIUtility.IconContent("ViewToolZoom").image, "Show the panel in the Scene view."), EditorStyles.miniButtonLeft, GUILayout.Width(66)))
                    UiFactory.Frame(rt);
                if (GUILayout.Button(new GUIContent("Select", "Select the panel itself (in the Hierarchy and here)."), EditorStyles.miniButtonRight, GUILayout.Width(52)))
                {
                    Select(panel.transform);
                    selected = null;
                }
            }

            DrawPanelHealth();

            if (panelOpen)
            {
                S.Line();
                EditorGUILayout.Space(6);
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("Name", panel.name);
                var size = rt.sizeDelta;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Size (mm)", "Width × height. 1 UI unit = 1 mm in the world."));
                    size.x = Mathf.Max(100, EditorGUILayout.FloatField(size.x));
                    GUILayout.Label("×", GUILayout.Width(12));
                    size.y = Mathf.Max(100, EditorGUILayout.FloatField(size.y));
                    if (GUILayout.Button(GUIContent.none, EditorStyles.popup, GUILayout.Width(20)))
                    {
                        var menu = new GenericMenu();
                        foreach (var p in SizePresets)
                        {
                            var s = p.size;
                            menu.AddItem(new GUIContent(p.label), false, () =>
                            {
                                Undo.RecordObject(rt, "Panel size");
                                rt.sizeDelta = s;
                            });
                        }
                        menu.ShowAsContext();
                    }
                }
                var bg = panel.GetComponent<Image>();
                Color bgColor = bg ? EditorGUILayout.ColorField("Background", bg.color) : default;
                var layout = panel.GetComponent<VerticalLayoutGroup>();
                float padding = layout ? EditorGUILayout.Slider(new GUIContent("Edge padding", "Space between the panel's edge and its content."), layout.padding.left, 0, 120) : 0;
                float spacing = layout ? EditorGUILayout.Slider(new GUIContent("Gap between items"), layout.spacing, 0, 80) : 0;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(panel.gameObject, "Rename panel");
                    panel.name = name;
                    Undo.RecordObject(rt, "Panel size");
                    rt.sizeDelta = size;
                    if (bg) { Undo.RecordObject(bg, "Panel colour"); bg.color = bgColor; }
                    if (layout)
                    {
                        Undo.RecordObject(layout, "Panel layout");
                        int p = Mathf.RoundToInt(padding);
                        layout.padding = new RectOffset(p, p, p, p);
                        layout.spacing = spacing;
                        LayoutRebuilder.MarkLayoutForRebuild(rt);
                    }
                    panelsDirty = true;
                }

                G.Group("Theme — click to re-colour everything");
                var currentId = ThemeIdOf(panel);
                var picked = DrawThemePicker(currentId);
                if (picked != currentId)
                {
                    UiFactory.ApplyTheme(panel, UiTheme.Get(picked), UiTheme.Get(currentId));
                    SetThemeId(panel, picked);
                }
            }
            G.EndCard();
        }

        /// <summary>The basics VRChat needs on a world canvas, with one-click fixes.</summary>
        private void DrawPanelHealth()
        {
            var go = panel.gameObject;
            var problems = new List<(string text, System.Action fix)>();
            if (panel.renderMode != RenderMode.WorldSpace)
                problems.Add(("Canvas isn't World Space", () => { Undo.RecordObject(panel, "Fix"); panel.renderMode = RenderMode.WorldSpace; }));
            if (!go.GetComponent<GraphicRaycaster>())
                problems.Add(("No GraphicRaycaster — nothing can be clicked", () => Undo.AddComponent<GraphicRaycaster>(go)));
            if (UiFactory.UiShapeType != null && !go.GetComponent(UiFactory.UiShapeType))
                problems.Add(("No VRCUiShape — the VRChat laser won't hit it", () => Undo.AddComponent(go, UiFactory.UiShapeType)));
            if (go.layer != UiFactory.UiLayer)
                problems.Add(("Not on the UI layer", () => { Undo.RegisterFullObjectHierarchyUndo(go, "Fix layer"); UiFactory.SetLayer(go.transform, UiFactory.UiLayer); }));

            if (problems.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Chip("✓ VRChat-ready canvas", S.Success, "World Space, GraphicRaycaster, VRCUiShape and UI layer are all set.");
                    G.Chip(Mathf.RoundToInt(((RectTransform)panel.transform).sizeDelta.x / 10f) + " × " + Mathf.RoundToInt(((RectTransform)panel.transform).sizeDelta.y / 10f) + " cm");
                    G.Chip(UiTheme.DisplayName(ThemeIdOf(panel)));
                    GUILayout.FlexibleSpace();
                }
                return;
            }
            foreach (var p in problems)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Report(p.text, false);
                    if (GUILayout.Button("Fix", GUILayout.Width(44))) p.fix();
                }
            }
        }
    }
}

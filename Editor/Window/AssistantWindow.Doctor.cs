using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        [SerializeField] private bool doctorAllPanels = true;
        [SerializeField] private UiThemeId templateTheme = UiThemeId.NekoDark;
        [SerializeField] private string templateCategory;
        [SerializeField] private string qrText = "https://example.com";
        private List<Finding> findings;
        private List<string> importNotes;

        internal static void RememberTheme(Canvas c, UiThemeId id)
        {
            EditorPrefs.SetInt(ThemePrefPrefix + GlobalObjectId.GetGlobalObjectIdSlow(c), (int)id);
            EditorPrefs.SetInt(LastThemePref, (int)id);
        }

        // ------------------------------------------------------------------ Doctor tab

        private void DrawDoctor()
        {
            G.BeginCard("d_console.warnicon.sml", "UI Doctor", "Finds common problems in world UI – made by the Assistant or by hand – and fixes them in one click. Every fix can be undone.");
            using (new EditorGUILayout.HorizontalScope())
            {
                int scope = GUILayout.Toolbar(doctorAllPanels ? 0 : 1, new[] { "All panels in the scene", "Only " + (panel ? panel.name : "the selected panel") });
                doctorAllPanels = scope == 0;
            }
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (G.Action("Search Icon", "Check now", doctorAllPanels || panel, true)) RunDoctor();
                var fixable = findings?.Where(f => f.fix != null).ToList();
                using (new EditorGUI.DisabledScope(fixable == null || fixable.Count == 0))
                    if (GUILayout.Button("Fix all (" + (fixable?.Count ?? 0) + ")", S.BigButton, GUILayout.Width(110)))
                    {
                        Undo.IncrementCurrentGroup();
                        int group = Undo.GetCurrentGroup();
                        foreach (var f in fixable) f.fix();
                        Undo.CollapseUndoOperations(group);
                        Undo.SetCurrentGroupName("UI Doctor: fix all");
                        RunDoctor();
                    }
            }
            G.EndCard();

            if (findings == null) return;
            foreach (var f in findings)
            {
                G.BeginCard(null, null);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string icon = f.severity == Severity.Error ? "console.erroricon.sml" : f.severity == Severity.Warning ? "console.warnicon.sml" : "console.infoicon.sml";
                    GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label(f.title, EditorStyles.boldLabel, GUILayout.MinWidth(60));
                    GUILayout.FlexibleSpace();
                    if (f.objects != null && f.objects.Length > 0 && GUILayout.Button(new GUIContent("Select", "Select the objects this is about."), EditorStyles.miniButtonLeft, GUILayout.Width(50)))
                    {
                        Selection.objects = f.objects.Where(o => o).ToArray();
                        if (Selection.activeGameObject) EditorGUIUtility.PingObject(Selection.activeGameObject);
                    }
                    if (f.fix != null && GUILayout.Button(f.fixLabel, EditorStyles.miniButtonRight, GUILayout.Width(Mathf.Max(44, f.fixLabel.Length * 7 + 14))))
                    {
                        f.fix();
                        RunDoctor();
                        GUIUtility.ExitGUI();
                    }
                }
                if (!string.IsNullOrEmpty(f.detail)) GUILayout.Label(f.detail, S.Hint);
                G.EndCard();
            }
        }

        private void RunDoctor()
        {
            var canvases = doctorAllPanels ? LessonBook.WorldCanvases() : new[] { panel };
            findings = UiDoctor.Scan(canvases);
        }

        // ------------------------------------------------------------------ Templates tab

        private void DrawTemplates()
        {
            G.BeginCard("Prefab Icon", "Templates", "Ready-made panels and systems. Drop one in, then change anything with the Build tab. Each has a lesson that ends with you building a simpler one by hand.");
            G.Group("Look", true);
            templateTheme = DrawThemePicker(templateTheme);
            var categories = TemplateLibrary.All.Select(t => t.category).Distinct().ToList();
            EditorGUILayout.Space(4);
            int cat = Mathf.Max(0, categories.IndexOf(templateCategory));
            cat = GUILayout.Toolbar(cat, categories.ToArray());
            templateCategory = categories[cat];
            G.EndCard();

            if (templateCategory == "Media") DrawCommunityPlayers();

            foreach (var t in TemplateLibrary.All.Where(x => x.category == templateCategory))
            {
                G.BeginCard(t.icon, t.title);
                GUILayout.Label(t.description, EditorStyles.wordWrappedLabel);
                if (t.id == "links") DrawLinksEditor();
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    var lesson = string.IsNullOrEmpty(t.lessonId) ? null : LessonBook.Get(t.lessonId);
                    if (lesson != null && GUILayout.Button(new GUIContent((LessonBook.IsDone(lesson) ? "✓ " : "") + "Lesson", lesson.summary), GUILayout.Width(80), GUILayout.Height(26)))
                        StartLesson(lesson);
                    GUILayout.FlexibleSpace();
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = S.Accent;
                    if (GUILayout.Button(S.Icon("Toolbar Plus", " Create"), GUILayout.Width(110), GUILayout.Height(26)))
                    {
                        try
                        {
                            var canvas = TemplateLibrary.Create(t, templateTheme);
                            panel = canvas;
                            selected = null;
                            showWizard = false;
                            panelsDirty = true;
                            mainTab = MainTab.Build;
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogException(e);
                            EditorUtility.DisplayDialog("World UI Assistant", "Couldn't create \"" + t.title + "\":\n" + e.Message, "OK");
                        }
                        GUIUtility.ExitGUI();
                    }
                    GUI.backgroundColor = old;
                }
                G.EndCard();
            }

            DrawQrMaker();
            DrawBlueprints();
        }

        private void DrawLinksEditor()
        {
            var links = TemplateLibrary.Links;
            int remove = -1;
            for (int i = 0; i < links.Count; i++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    string n = EditorGUILayout.TextField(links[i].name, GUILayout.Width(110));
                    string u = EditorGUILayout.TextField(links[i].url);
                    links[i] = (n, u);
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                }
            if (remove >= 0) links.RemoveAt(remove);
            if (GUILayout.Button("+ Link", EditorStyles.miniButton, GUILayout.Width(70))) links.Add(("Link", "https://"));
        }

        private void DrawQrMaker()
        {
            G.BeginCard("d_Linked", "QR code maker", "Udon can't open web pages, so show links as QR codes players scan with their phone.");
            qrText = EditorGUILayout.TextField("Link or text", qrText);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(qrText)))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Save QR picture", "Saves a PNG sprite in " + TemplateLibrary.GeneratedFolder + "/QR"), GUILayout.Height(24)))
                {
                    var sprite = MakeQr();
                    if (sprite) { Selection.activeObject = sprite; EditorGUIUtility.PingObject(sprite); }
                }
                using (new EditorGUI.DisabledScope(!panel))
                    if (GUILayout.Button(new GUIContent("Add to panel", "Adds the QR picture to the current panel (after the selected item)."), GUILayout.Height(24)))
                    {
                        var sprite = MakeQr();
                        if (sprite)
                        {
                            var target = AddTarget();
                            var go = UiFactory.Create(WidgetKind.Image, target.parent, target.index, CurrentTheme);
                            go.name = "QR Code";
                            var img = go.GetComponent<Image>();
                            img.sprite = sprite;
                            img.color = Color.white;
                            go.GetComponent<LayoutElement>().preferredHeight = 260;
                            mainTab = MainTab.Build;
                            Select(go.transform);
                        }
                    }
            }
            G.EndCard();
        }

        private Sprite MakeQr()
        {
            try
            {
                string name = TemplateLibrary.SafeFileName(qrText.Replace("https://", "").Replace("http://", ""));
                if (name.Length > 40) name = name.Substring(0, 40);
                return QrCode.SaveSprite(qrText, TemplateLibrary.GeneratedFolder + "/QR/" + name + ".png");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("QR code", e.Message, "OK");
                return null;
            }
        }

        private void DrawBlueprints()
        {
            G.BeginCard("TextAsset Icon", "Blueprints (JSON)", "Save a panel's layout, text, colours and actions as JSON to share it or rebuild it in another project.");
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!panel))
                {
                    if (GUILayout.Button(new GUIContent("Export…", "Save the current panel as a .json blueprint."), GUILayout.Height(24)))
                    {
                        string path = EditorUtility.SaveFilePanel("Export blueprint", "", TemplateLibrary.SafeFileName(panel.name) + ".json", "json");
                        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, Blueprint.Export(panel, ThemeIdOf(panel)));
                    }
                    if (GUILayout.Button(new GUIContent("Copy JSON", "Copy the blueprint to the clipboard."), GUILayout.Height(24)))
                    {
                        EditorGUIUtility.systemCopyBuffer = Blueprint.Export(panel, ThemeIdOf(panel));
                        ShowNotification(new GUIContent("Blueprint copied"));
                    }
                }
                if (GUILayout.Button(new GUIContent("Import…", "Build a new panel from a .json blueprint."), GUILayout.Height(24)))
                {
                    string path = EditorUtility.OpenFilePanel("Import blueprint", "", "json");
                    if (!string.IsNullOrEmpty(path)) ImportBlueprint(File.ReadAllText(path));
                }
                if (GUILayout.Button(new GUIContent("Paste", "Build a new panel from a blueprint on the clipboard."), GUILayout.Height(24)))
                    ImportBlueprint(EditorGUIUtility.systemCopyBuffer);
            }
            if (importNotes != null)
                foreach (var n in importNotes) G.Report(n, false);
            G.EndCard();
        }

        private void ImportBlueprint(string json)
        {
            try
            {
                var canvas = Blueprint.Import(json, out importNotes);
                panel = canvas;
                selected = null;
                showWizard = false;
                panelsDirty = true;
                UiFactory.Frame((RectTransform)canvas.transform);
                if (importNotes.Count == 0) { importNotes = null; mainTab = MainTab.Build; }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Import blueprint", "Couldn't import: " + e.Message, "OK");
            }
            GUIUtility.ExitGUI();
        }
    }
}

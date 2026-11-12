using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// JSON blueprint of a panel: layout, text, colours, sizes and actions. Share it, keep it in git,
    /// or rebuild the panel in another project. Objects outside the panel (sounds, teleport points,
    /// mirrors) are stored by their Hierarchy path and reconnected on import when they exist.
    /// </summary>
    [Serializable]
    internal class BlueprintFile
    {
        public string format = "nekosune-world-ui-blueprint";
        public int version = 1;
        public string name;
        public Vector2 size;
        public string theme;
        public string background;
        public float padding;
        public float spacing;
        public List<BlueprintItem> items = new List<BlueprintItem>();
    }

    [Serializable]
    internal class BlueprintItem
    {
        public int parent = -1;              // index in items, -1 = the panel
        public string kind;
        public string name;
        public bool active = true;
        public string text;
        public float fontSize;
        public bool bold;
        public string align;
        public string color;                 // text or image colour
        public string fill;                  // button/box colour
        public float height = -1;
        public float spacing = -1;
        public bool stretch = true;
        public int columns;
        public Vector2 cell;
        public float min, max, value;
        public bool whole, isOn;
        public string sprite;                // asset path
        public BlueprintAction action;
    }

    [Serializable]
    internal class BlueprintAction
    {
        public string choice;
        public bool synced;
        public List<string> targets = new List<string>();
        public string sound, teleport, page, eventTarget, eventName, toastText;
    }

    internal static class Blueprint
    {
        // ------------------------------------------------------------------ export

        public static string Export(Canvas panel, UiThemeId theme)
        {
            var root = panel.transform;
            var file = new BlueprintFile
            {
                name = panel.name,
                size = ((RectTransform)root).sizeDelta,
                theme = theme.ToString(),
                background = root.GetComponent<Image>() ? Hex(root.GetComponent<Image>().color) : null,
            };
            var v = root.GetComponent<VerticalLayoutGroup>();
            if (v) { file.padding = v.padding.left; file.spacing = v.spacing; }

            var index = new Dictionary<Transform, int>();
            void Walk(Transform content, int parent)
            {
                for (int i = 0; i < content.childCount; i++)
                {
                    var t = content.GetChild(i);
                    var kind = UiWidgets.Detect(t);
                    if (kind == WidgetKind.Other) continue;
                    var item = Describe(t, kind);
                    item.parent = parent;
                    index[t] = file.items.Count;
                    file.items.Add(item);
                    var inner = UiWidgets.ContentOf(t);
                    if (inner) Walk(inner, index[t]);
                }
            }
            Walk(root, -1);

            // Actions last, so page references can point at item indexes.
            foreach (var kv in index)
            {
                var a = kv.Key.GetComponent<NekoUiAction>();
                if (!a) continue;
                string Ref(GameObject go) => !go ? null : index.TryGetValue(go.transform, out int i) ? "#" + i : PathOf(go.transform);
                file.items[kv.Value].action = new BlueprintAction
                {
                    choice = UiActions.ChoiceOf(a).ToString(),
                    synced = a.synced,
                    targets = (a.targets ?? new GameObject[0]).Select(Ref).ToList(),
                    sound = a.sound ? PathOf(a.sound.transform) : null,
                    teleport = a.teleportTarget ? PathOf(a.teleportTarget) : null,
                    page = Ref(a.page),
                    eventTarget = a.eventTarget ? PathOf(a.eventTarget.transform) : null,
                    eventName = a.eventName,
                    toastText = a.toastText,
                };
            }
            return JsonUtility.ToJson(file, true);
        }

        private static BlueprintItem Describe(Transform t, WidgetKind kind)
        {
            var item = new BlueprintItem { kind = kind.ToString(), name = t.name, active = t.gameObject.activeSelf };
            var le = t.GetComponent<LayoutElement>();
            if (le) item.height = le.preferredHeight >= 0 ? le.preferredHeight : le.minHeight;
            switch (kind)
            {
                case WidgetKind.Text:
                    var tmp = t.GetComponent<TMP_Text>();
                    if (tmp)
                    {
                        item.text = tmp.text; item.fontSize = tmp.fontSize; item.bold = (tmp.fontStyle & FontStyles.Bold) != 0;
                        item.align = tmp.alignment.ToString(); item.color = Hex(tmp.color);
                    }
                    break;
                case WidgetKind.Button:
                    var label = t.GetComponentInChildren<TMP_Text>(true);
                    if (label) { item.text = label.text; item.fontSize = label.fontSize; item.color = Hex(label.color); }
                    var img = t.GetComponent<Button>().targetGraphic;
                    if (img) item.fill = Hex(img.color);
                    break;
                case WidgetKind.Toggle:
                    var toggle = t.GetComponent<Toggle>();
                    var tl = t.GetComponentInChildren<TMP_Text>(true);
                    if (tl) item.text = tl.text;
                    item.isOn = toggle.isOn;
                    if (toggle.targetGraphic) item.fill = Hex(toggle.targetGraphic.color);
                    if (toggle.graphic) item.color = Hex(toggle.graphic.color);
                    break;
                case WidgetKind.Slider:
                    var s = t.GetComponent<Slider>();
                    item.min = s.minValue; item.max = s.maxValue; item.value = s.value; item.whole = s.wholeNumbers;
                    break;
                case WidgetKind.Image:
                case WidgetKind.Divider:
                    var image = t.GetComponent<Image>();
                    item.color = Hex(image.color);
                    if (image.sprite) item.sprite = AssetDatabase.GetAssetPath(image.sprite);
                    break;
                case WidgetKind.Row:
                case WidgetKind.Column:
                case WidgetKind.Page:
                    var g = t.GetComponent<HorizontalOrVerticalLayoutGroup>();
                    item.spacing = g.spacing;
                    item.stretch = kind == WidgetKind.Row ? g.childForceExpandWidth : g.childForceExpandHeight;
                    break;
                case WidgetKind.Grid:
                    var grid = t.GetComponent<GridLayoutGroup>();
                    item.columns = grid.constraintCount; item.cell = grid.cellSize; item.spacing = grid.spacing.x;
                    break;
                case WidgetKind.ScrollList:
                    var sr = t.GetComponent<ScrollRect>();
                    var cv = sr.content ? sr.content.GetComponent<VerticalLayoutGroup>() : null;
                    if (cv) item.spacing = cv.spacing;
                    break;
            }
            return item;
        }

        // ------------------------------------------------------------------ import

        /// <summary>Builds a new panel from a blueprint. Returns the panel and notes about anything not reconnected.</summary>
        public static Canvas Import(string json, out List<string> notes)
        {
            var log = new List<string>();
            notes = log;
            var file = JsonUtility.FromJson<BlueprintFile>(json);
            if (file == null || file.format != "nekosune-world-ui-blueprint") throw new ArgumentException("This isn't a World UI Assistant blueprint.");
            if (!Enum.TryParse(file.theme, out UiThemeId themeId)) themeId = UiThemeId.NekoDark;
            var theme = UiTheme.Get(themeId);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var b = UiBuilder.NewPanel(file.name, file.size, themeId);
            var root = b.panel.transform;
            if (file.background != null && root.GetComponent<Image>()) root.GetComponent<Image>().color = Color(file.background);
            var pv = root.GetComponent<VerticalLayoutGroup>();
            if (pv && file.spacing > 0) { int p = Mathf.RoundToInt(file.padding); pv.padding = new RectOffset(p, p, p, p); pv.spacing = file.spacing; }

            var made = new Transform[file.items.Count];
            for (int i = 0; i < file.items.Count; i++)
            {
                var item = file.items[i];
                Transform parent = item.parent < 0 ? root : UiWidgets.ContentOf(made[item.parent]) ?? made[item.parent];
                if (!Enum.TryParse(item.kind, out WidgetKind kind)) { notes.Add("Skipped unknown item type " + item.kind); continue; }
                b.parent = parent;
                made[i] = Build(b, kind, item, theme, notes);
                if (made[i]) made[i].gameObject.SetActive(item.active);
            }

            for (int i = 0; i < file.items.Count; i++)
            {
                var item = file.items[i];
                if (item.action == null || !made[i] || string.IsNullOrEmpty(item.action.choice)) continue;
                var control = made[i].GetComponent<Selectable>();
                if (!control || !Enum.TryParse(item.action.choice, out ActionChoice choice) || choice == ActionChoice.Nothing) continue;
                var ba = item.action;
                GameObject Resolve(string r)
                {
                    if (string.IsNullOrEmpty(r)) return null;
                    if (r.StartsWith("#") && int.TryParse(r.Substring(1), out int idx) && idx < made.Length && made[idx]) return made[idx].gameObject;
                    var found = FindByPath(r);
                    if (!found) log.Add("\"" + item.name + "\": couldn't find " + r + " in this scene – pick it again.");
                    return found;
                }
                var a = UiActions.SetChoice(control, choice);
                UiActions.Edit(a, "Import blueprint", x =>
                {
                    x.synced = ba.synced && UiActions.CanSync(choice);
                    x.targets = ba.targets.Select(Resolve).ToArray();
                    var sound = Resolve(ba.sound);
                    x.sound = sound ? sound.GetComponent<AudioSource>() : null;
                    var tp = Resolve(ba.teleport);
                    x.teleportTarget = tp ? tp.transform : null;
                    x.page = Resolve(ba.page);
                    var et = Resolve(ba.eventTarget);
                    x.eventTarget = et ? et.GetComponent<UdonBehaviour>() : null;
                    x.eventName = ba.eventName;
                    x.toastText = ba.toastText;
                    if (choice == ActionChoice.OpenPage) UiActions.RefreshPages(x);
                });
            }
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Import blueprint");
            return b.panel;
        }

        private static Transform Build(UiBuilder b, WidgetKind kind, BlueprintItem item, UiTheme theme, List<string> notes)
        {
            Transform t;
            switch (kind)
            {
                case WidgetKind.Text:
                    var tmp = b.Text(item.text ?? "", TextStyle.Body, item.name);
                    tmp.fontSize = item.fontSize > 0 ? item.fontSize : tmp.fontSize;
                    tmp.fontStyle = item.bold ? FontStyles.Bold : FontStyles.Normal;
                    if (Enum.TryParse(item.align, out TextAlignmentOptions al)) tmp.alignment = al;
                    if (item.color != null) tmp.color = Color(item.color);
                    t = tmp.transform;
                    break;
                case WidgetKind.Button:
                    var btn = b.Button(item.text ?? "Button", item.name);
                    var label = btn.GetComponentInChildren<TMP_Text>();
                    if (item.fontSize > 0) label.fontSize = item.fontSize;
                    if (item.color != null) label.color = Color(item.color);
                    if (item.fill != null && btn.targetGraphic) btn.targetGraphic.color = Color(item.fill);
                    t = btn.transform;
                    break;
                case WidgetKind.Toggle:
                    var tg = b.Toggle(item.text ?? "Toggle", item.isOn, item.name);
                    if (item.fill != null && tg.targetGraphic) tg.targetGraphic.color = Color(item.fill);
                    if (item.color != null && tg.graphic) tg.graphic.color = Color(item.color);
                    t = tg.transform;
                    break;
                case WidgetKind.Slider:
                    var s = b.Slider(item.min, item.max, item.value, item.name);
                    s.wholeNumbers = item.whole;
                    t = s.transform;
                    break;
                case WidgetKind.Image:
                case WidgetKind.Divider:
                    var go = UiFactory.Create(kind, b.parent, -1, theme);
                    go.name = item.name;
                    var image = go.GetComponent<Image>();
                    if (item.color != null) image.color = Color(item.color);
                    if (!string.IsNullOrEmpty(item.sprite))
                    {
                        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(item.sprite);
                        if (!image.sprite) notes.Add("\"" + item.name + "\": picture " + item.sprite + " isn't in this project.");
                    }
                    t = go.transform;
                    break;
                case WidgetKind.Page:
                    t = b.Page(item.name, item.active, null);
                    break;
                case WidgetKind.Spacer:
                case WidgetKind.Row:
                case WidgetKind.Column:
                case WidgetKind.Grid:
                case WidgetKind.ScrollList:
                    t = UiFactory.Create(kind, b.parent, -1, theme).transform;
                    t.name = item.name;
                    break;
                default:
                    return null;
            }

            var hg = t.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (hg && item.spacing >= 0)
            {
                hg.spacing = item.spacing;
                if (kind == WidgetKind.Row) hg.childForceExpandWidth = item.stretch; else hg.childForceExpandHeight = item.stretch;
            }
            var grid = t.GetComponent<GridLayoutGroup>();
            if (grid && item.columns > 0) { grid.constraintCount = item.columns; grid.cellSize = item.cell; grid.spacing = new Vector2(item.spacing, item.spacing); }
            if (kind == WidgetKind.ScrollList && item.spacing >= 0)
            {
                var cv = t.GetComponent<ScrollRect>().content.GetComponent<VerticalLayoutGroup>();
                if (cv) cv.spacing = item.spacing;
            }
            var le = t.GetComponent<LayoutElement>();
            if (le && item.height > 0)
            {
                if (le.preferredHeight >= 0) le.preferredHeight = item.height; else le.minHeight = item.height;
            }
            return t;
        }

        // ------------------------------------------------------------------ helpers

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x; x = x.parent) parts.Insert(0, x.name);
            return string.Join("/", parts);
        }

        /// <summary>Finds an object by Hierarchy path in the open scenes, including inactive objects.</summary>
        public static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;
                    var t = root.transform;
                    for (int i = 1; i < parts.Length && t; i++)
                    {
                        Transform next = null;
                        for (int c = 0; c < t.childCount; c++) if (t.GetChild(c).name == parts[i]) { next = t.GetChild(c); break; }
                        t = next;
                    }
                    if (t) return t.gameObject;
                }
            }
            return null;
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
        private static Color Color(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : UnityEngine.Color.white;
    }
}

using System;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Small helper used by templates and ready-made systems to build panels in a few readable lines.
    /// Everything goes through UiFactory/UiActions, so it is normal Unity UI and undoable.
    /// </summary>
    internal class UiBuilder
    {
        public readonly Canvas panel;
        public readonly UiTheme theme;
        public Transform parent;

        private UiBuilder(Canvas panel, UiTheme theme)
        {
            this.panel = panel;
            this.theme = theme;
            parent = panel.transform;
        }

        public static UiBuilder NewPanel(string name, Vector2 size, UiThemeId themeId, PanelPlacement placement = PanelPlacement.InFrontOfSceneCamera)
        {
            var theme = UiTheme.Get(themeId);
            var canvas = UiFactory.CreatePanel(name, size, theme, placement);
            AssistantWindow.RememberTheme(canvas, themeId);
            return new UiBuilder(canvas, theme);
        }

        // ------------------------------------------------------------------ text

        public TMP_Text Text(string text, TextStyle style = TextStyle.Body, string name = null)
        {
            var go = UiFactory.Create(WidgetKind.Text, parent, -1, theme);
            var tmp = go.GetComponent<TMP_Text>();
            UiFactory.StyleText(tmp, style, theme);
            tmp.text = text;
            go.name = name ?? (style == TextStyle.Title ? "Title" : style == TextStyle.Heading ? "Heading" : style == TextStyle.Caption ? "Caption" : "Text");
            return tmp;
        }

        public TMP_Text Title(string text) => Text(text, TextStyle.Title);
        public TMP_Text Heading(string text) => Text(text, TextStyle.Heading);
        public TMP_Text Caption(string text) => Text(text, TextStyle.Caption);

        // ------------------------------------------------------------------ controls

        public Button Button(string label, string name = null)
        {
            var go = UiFactory.Create(WidgetKind.Button, parent, -1, theme);
            go.name = name ?? label;
            go.GetComponentInChildren<TMP_Text>().text = label;
            return go.GetComponent<Button>();
        }

        public Toggle Toggle(string label, bool on, string name = null)
        {
            var go = UiFactory.Create(WidgetKind.Toggle, parent, -1, theme);
            go.name = name ?? label;
            go.GetComponentInChildren<TMP_Text>().text = label;
            var t = go.GetComponent<Toggle>();
            t.isOn = on;
            if (t.graphic) t.graphic.canvasRenderer.SetAlpha(on ? 1f : 0f);
            return t;
        }

        public Slider Slider(float min, float max, float value, string name = "Slider")
        {
            var go = UiFactory.Create(WidgetKind.Slider, parent, -1, theme);
            go.name = name;
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            return s;
        }

        public Image Image(Sprite sprite, float height, string name = "Image")
        {
            var go = UiFactory.Create(WidgetKind.Image, parent, -1, theme);
            go.name = name;
            var img = go.GetComponent<Image>();
            if (sprite) { img.sprite = sprite; img.color = Color.white; }
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return img;
        }

        /// <summary>A text field players can type in (TextMeshPro input field).</summary>
        public TMP_InputField Input(string placeholder, string name = "Input", float height = 56f)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpResources);
            go.name = name;
            var field = go.GetComponent<TMP_InputField>();
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            if (field.placeholder is TMP_Text ph) { ph.text = placeholder; ph.fontSize = theme.fontSize * 0.9f; }
            if (field.textComponent) { field.textComponent.fontSize = theme.fontSize; field.textComponent.color = Color.black; }
            field.pointSize = theme.fontSize;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            Place(go);
            return field;
        }

        public void Divider() => UiFactory.Create(WidgetKind.Divider, parent, -1, theme);

        public void Spacer(float height)
        {
            var go = UiFactory.Create(WidgetKind.Spacer, parent, -1, theme);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
        }

        // ------------------------------------------------------------------ containers

        public Transform Row(Action inside, string name = "Row") => Inside(UiFactory.Create(WidgetKind.Row, parent, -1, theme).transform, name, inside);

        public Transform Grid(int columns, Vector2 cell, Action inside, string name = "Grid")
        {
            var t = UiFactory.Create(WidgetKind.Grid, parent, -1, theme).transform;
            var g = t.GetComponent<GridLayoutGroup>();
            g.constraintCount = columns;
            g.cellSize = cell;
            return Inside(t, name, inside);
        }

        public Transform Scroll(float height, Action inside, string name = "Scroll List")
        {
            var go = UiFactory.Create(WidgetKind.ScrollList, parent, -1, theme);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            go.name = name;
            var content = go.GetComponent<ScrollRect>().content;
            var old = parent;
            parent = content;
            inside?.Invoke();
            parent = old;
            return go.transform;
        }

        /// <summary>A page directly under the panel (only one page is shown at a time).</summary>
        public Transform Page(string name, bool shown, Action inside)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = panel.gameObject.layer;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = theme.spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            go.transform.SetParent(panel.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Add page");
            var old = parent;
            parent = go.transform;
            inside?.Invoke();
            parent = old;
            go.SetActive(shown);
            return go.transform;
        }

        /// <summary>A plain column (vertical group) inside the current parent, e.g. a product card.</summary>
        public Transform Column(Action inside, string name = "Column", Color? background = null, float padding = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = theme.spacing * 0.5f;
            int p = Mathf.RoundToInt(padding);
            v.padding = new RectOffset(p, p, p, p);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (background.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = background.Value;
                img.raycastTarget = false;
            }
            Place(go);
            var old = parent;
            parent = go.transform;
            inside?.Invoke();
            parent = old;
            return go.transform;
        }

        private Transform Inside(Transform t, string name, Action inside)
        {
            t.name = name;
            // Rows/grids from UiFactory start empty here (the Add card adds sample buttons, templates don't).
            var old = parent;
            parent = t;
            inside?.Invoke();
            parent = old;
            return t;
        }

        private void Place(GameObject go)
        {
            go.transform.SetParent(parent, false);
            UiFactory.SetLayer(go.transform, panel.gameObject.layer);
            Undo.RegisterCreatedObjectUndo(go, "Add " + go.name);
        }

        // ------------------------------------------------------------------ Udon wiring

        public NekoUiAction Action(Selectable control, ActionChoice choice, Action<NekoUiAction> set = null)
        {
            var a = UiActions.SetChoice(control, choice);
            if (set != null) UiActions.Edit(a, "Template", set);
            return a;
        }

        /// <summary>Adds an UdonSharp script to an object (with its hidden UdonBehaviour).</summary>
        public static T AddUdon<T>(GameObject go) where T : UdonSharpBehaviour => UdonSharpUndo.AddComponent<T>(go);

        public static void Set<T>(T u, Action<T> set) where T : UdonSharpBehaviour
        {
            Undo.RecordObject(u, "Set " + typeof(T).Name);
            set(u);
            UdonSharpEditorUtility.CopyProxyToUdon(u);
            EditorUtility.SetDirty(u);
        }

        /// <summary>Button.onClick → target UdonBehaviour.SendCustomEvent(eventName).</summary>
        public static void OnClick(Button b, UdonSharpBehaviour target, string eventName)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
            Undo.RecordObject(b, "Wire");
            UnityEventTools.AddStringPersistentListener(b.onClick, udon.SendCustomEvent, eventName);
        }

        public static void OnChange(Toggle t, UdonSharpBehaviour target, string eventName)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
            Undo.RecordObject(t, "Wire");
            UnityEventTools.AddStringPersistentListener(t.onValueChanged, udon.SendCustomEvent, eventName);
        }

        public static void OnChange(Slider s, UdonSharpBehaviour target, string eventName)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
            Undo.RecordObject(s, "Wire");
            UnityEventTools.AddStringPersistentListener(s.onValueChanged, udon.SendCustomEvent, eventName);
        }

        public static void OnEndEdit(TMP_InputField f, UdonSharpBehaviour target, string eventName)
        {
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
            Undo.RecordObject(f, "Wire");
            UnityEventTools.AddStringPersistentListener(f.onEndEdit, udon.SendCustomEvent, eventName);
        }

        public void Frame() => UiFactory.Frame((RectTransform)panel.transform);

        private static TMP_DefaultControls.Resources tmpResources;
        private static TMP_DefaultControls.Resources TmpResources
        {
            get
            {
                if (tmpResources.standard == null)
                {
                    tmpResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    tmpResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                    tmpResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
                }
                return tmpResources;
            }
        }
    }
}

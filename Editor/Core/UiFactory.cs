using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NekoSune.WorldUiAssistant
{
    internal enum PanelPlacement { InFrontOfSceneCamera, WorldOrigin, UnderSelection }

    /// <summary>Creates VRChat-ready world UI with plain Unity components. Every call is one Undo step.</summary>
    internal static class UiFactory
    {
        public const float PixelsToMetres = 0.001f; // 1 UI unit = 1 mm

        private static DefaultControls.Resources resources;
        private static TMP_DefaultControls.Resources tmpResources;

        private static void EnsureResources()
        {
            if (resources.standard != null) return;
            resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
            tmpResources = new TMP_DefaultControls.Resources
            {
                standard = resources.standard,
                background = resources.background,
                inputField = resources.inputField,
                knob = resources.knob,
                checkmark = resources.checkmark,
                dropdown = resources.dropdown,
                mask = resources.mask,
            };
        }

        // ------------------------------------------------------------------ environment

        private static Type uiShapeType;
        private static bool uiShapeSearched;

        /// <summary>VRChat's VRCUiShape (lets the VRChat laser hit the canvas); null outside a VRChat world project.</summary>
        public static Type UiShapeType
        {
            get
            {
                if (!uiShapeSearched)
                {
                    uiShapeSearched = true;
                    uiShapeType = TypeCache.GetTypesDerivedFrom<Component>().FirstOrDefault(t => t.Name == "VRCUiShape");
                }
                return uiShapeType;
            }
        }

        public static bool HasTmpFont => TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null;

        public static int UiLayer => LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;

        // ------------------------------------------------------------------ panel

        public static Canvas CreatePanel(string name, Vector2 size, UiTheme theme, PanelPlacement placement)
        {
            EnsureResources();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            var go = new GameObject(string.IsNullOrEmpty(name) ? "World UI Panel" : name, typeof(RectTransform));
            go.layer = UiLayer;
            var parent = placement == PanelPlacement.UnderSelection && Selection.activeTransform ? Selection.activeTransform : null;
            StageUtility.PlaceGameObjectInCurrentStage(go);
            if (parent) go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1f;
            go.AddComponent<GraphicRaycaster>();
            if (UiShapeType != null) go.AddComponent(UiShapeType);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * PixelsToMetres;
            if (!parent)
            {
                if (placement == PanelPlacement.InFrontOfSceneCamera && SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.camera)
                {
                    var cam = SceneView.lastActiveSceneView.camera.transform;
                    var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                    forward.Normalize();
                    rt.position = cam.position + forward * 2f;
                    rt.rotation = Quaternion.LookRotation(forward, Vector3.up); // text reads correctly from the camera side
                }
                else
                {
                    rt.position = new Vector3(0f, 1.5f, 0f);
                }
            }

            var bg = go.AddComponent<Image>();
            bg.sprite = resources.background;
            bg.type = Image.Type.Sliced;
            bg.color = theme.background;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(theme.padding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = theme.spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Undo.RegisterCreatedObjectUndo(go, "Create World UI Panel");
            EnsureEventSystem();
            Undo.CollapseUndoOperations(group);
            return canvas;
        }

        /// <summary>Same as the VRChat SDK: an EventSystem lets you click the UI in Play mode in the editor.</summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>(true)) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            StageUtility.PlaceGameObjectInCurrentStage(es);
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>Creates a widget under <paramref name="parent"/> at <paramref name="index"/> (-1 = last).</summary>
        public static GameObject Create(WidgetKind kind, Transform parent, int index, UiTheme theme)
        {
            EnsureResources();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            GameObject go;
            switch (kind)
            {
                case WidgetKind.Text: go = CreateText("Text", "New text. Click it in the outline to change it.", TextStyle.Body, theme); break;
                case WidgetKind.Button: go = CreateButton(theme); break;
                case WidgetKind.Toggle: go = CreateToggle(theme); break;
                case WidgetKind.Slider: go = CreateSlider(theme); break;
                case WidgetKind.Image: go = CreateImage(theme); break;
                case WidgetKind.Divider: go = CreateDivider(theme); break;
                case WidgetKind.Spacer: go = CreateSpacer(); break;
                case WidgetKind.Row: go = CreateGroup<HorizontalLayoutGroup>("Row", theme); break;
                case WidgetKind.Column: go = CreateGroup<VerticalLayoutGroup>("Column", theme); break;
                case WidgetKind.Grid: go = CreateGrid(theme); break;
                case WidgetKind.ScrollList: go = CreateScrollList(theme); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }

            go.transform.SetParent(parent, false);
            if (index >= 0) go.transform.SetSiblingIndex(index);
            SetLayer(go.transform, parent.gameObject.layer);
            Undo.RegisterCreatedObjectUndo(go, "Add " + UiWidgets.Label(kind));
            Undo.CollapseUndoOperations(group);
            return go;
        }

        public static GameObject CreateTitle(Transform parent, int index, UiTheme theme)
        {
            Undo.IncrementCurrentGroup();
            var go = CreateText("Title", "My World", TextStyle.Title, theme);
            go.transform.SetParent(parent, false);
            if (index >= 0) go.transform.SetSiblingIndex(index);
            SetLayer(go.transform, parent.gameObject.layer);
            Undo.RegisterCreatedObjectUndo(go, "Add Title");
            return go;
        }

        private static GameObject CreateText(string name, string text, TextStyle style, UiTheme theme)
        {
            var go = TMP_DefaultControls.CreateText(tmpResources);
            go.name = name;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            StyleText(tmp, style, theme);
            return go;
        }

        public static void StyleText(TMP_Text tmp, TextStyle style, UiTheme theme)
        {
            tmp.fontSize = Mathf.Round(theme.fontSize * UiWidgets.StyleScale(style));
            tmp.fontStyle = style == TextStyle.Title || style == TextStyle.Heading ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = style == TextStyle.Title ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            tmp.color = style == TextStyle.Caption ? theme.mutedText : theme.text;
        }

        private static GameObject CreateButton(UiTheme theme)
        {
            var go = TMP_DefaultControls.CreateButton(tmpResources);
            go.name = "Button";
            var button = go.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = "Button";
            label.raycastTarget = false;
            Element(go, minHeight: theme.buttonHeight);
            ThemeButton(button, theme);
            return go;
        }

        private static GameObject CreateToggle(UiTheme theme)
        {
            var go = new GameObject("Toggle", typeof(RectTransform));
            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 16f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            Element(go, minHeight: theme.buttonHeight * 0.75f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            box.transform.SetParent(go.transform, false);
            var boxImage = box.GetComponent<Image>();
            boxImage.sprite = resources.standard;
            boxImage.type = Image.Type.Sliced;
            var boxLe = box.GetComponent<LayoutElement>();
            boxLe.minWidth = boxLe.preferredWidth = 44f;
            boxLe.minHeight = boxLe.preferredHeight = 44f;

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(box.transform, false);
            var checkRt = (RectTransform)check.transform;
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(6, 6);
            checkRt.offsetMax = new Vector2(-6, -6);
            var checkImage = check.GetComponent<Image>();
            checkImage.sprite = resources.checkmark;
            checkImage.raycastTarget = false;

            var label = CreateText("Label", "Toggle", TextStyle.Body, theme);
            label.transform.SetParent(go.transform, false);
            label.AddComponent<LayoutElement>().flexibleWidth = 1f;
            label.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            ThemeToggle(toggle, theme);
            return go;
        }

        private static GameObject CreateSlider(UiTheme theme)
        {
            var go = DefaultControls.CreateSlider(resources);
            go.name = "Slider";
            var slider = go.GetComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.value = 0.5f;
            Element(go, minHeight: 44f);
            var handle = slider.handleRect;
            if (handle) handle.sizeDelta = new Vector2(40f, 0f);
            ThemeSlider(slider, theme);
            return go;
        }

        private static GameObject CreateImage(UiTheme theme)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.color = theme.control;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Element(go, minHeight: 0f, preferredHeight: 240f);
            return go;
        }

        private static GameObject CreateDivider(UiTheme theme)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.color = WithAlpha(theme.mutedText, 0.35f);
            image.raycastTarget = false;
            Element(go, minHeight: 3f, preferredHeight: 3f);
            return go;
        }

        private static GameObject CreateSpacer()
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            Element(go, minHeight: 32f, preferredHeight: 32f);
            return go;
        }

        private static GameObject CreateGroup<T>(string name, UiTheme theme) where T : HorizontalOrVerticalLayoutGroup
        {
            var go = new GameObject(name, typeof(RectTransform));
            var g = go.AddComponent<T>();
            g.spacing = theme.spacing;
            g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return go;
        }

        private static GameObject CreateGrid(UiTheme theme)
        {
            var go = new GameObject("Grid", typeof(RectTransform));
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(220f, theme.buttonHeight);
            g.spacing = new Vector2(theme.spacing, theme.spacing);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            g.childAlignment = TextAnchor.UpperCenter;
            return go;
        }

        private static GameObject CreateScrollList(UiTheme theme)
        {
            var go = DefaultControls.CreateScrollView(resources);
            go.name = "Scroll List";
            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            if (sr.horizontalScrollbar) UnityEngine.Object.DestroyImmediate(sr.horizontalScrollbar.gameObject);
            sr.horizontalScrollbar = null;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            sr.verticalScrollbarSpacing = 4f;
            if (sr.verticalScrollbar)
            {
                ((RectTransform)sr.verticalScrollbar.transform).sizeDelta = new Vector2(16f, 0f);
                sr.verticalScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            var content = sr.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = theme.spacing;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Element(go, minHeight: 120f, preferredHeight: 360f);
            ThemeScrollList(sr, theme);
            return go;
        }

        // ------------------------------------------------------------------ theme

        /// <summary>Re-colours every widget in a panel with a theme (one Undo step). Text keeps its style.</summary>
        public static void ApplyTheme(Canvas panel, UiTheme theme, UiTheme previous)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var root = panel.transform;

            var bg = root.GetComponent<Image>();
            if (bg) { Undo.RecordObject(bg, "Apply theme"); bg.color = theme.background; }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                switch (UiWidgets.Detect(t))
                {
                    case WidgetKind.Text:
                        var tmp = t.GetComponent<TMP_Text>();
                        if (tmp && !InsideControl(t, root))
                        {
                            Undo.RecordObject(tmp, "Apply theme");
                            var style = UiWidgets.GuessStyle(tmp.fontSize, previous.fontSize);
                            tmp.fontSize = Mathf.Round(theme.fontSize * UiWidgets.StyleScale(style));
                            tmp.color = style == TextStyle.Caption ? theme.mutedText : theme.text;
                        }
                        break;
                    case WidgetKind.Button: ThemeButton(t.GetComponent<Button>(), theme); break;
                    case WidgetKind.Toggle: ThemeToggle(t.GetComponent<Toggle>(), theme); break;
                    case WidgetKind.Slider: ThemeSlider(t.GetComponent<Slider>(), theme); break;
                    case WidgetKind.ScrollList: ThemeScrollList(t.GetComponent<ScrollRect>(), theme); break;
                    case WidgetKind.Divider:
                        var d = t.GetComponent<Image>();
                        Undo.RecordObject(d, "Apply theme");
                        d.color = WithAlpha(theme.mutedText, 0.35f);
                        break;
                    case WidgetKind.Image:
                        var img = t.GetComponent<Image>();
                        if (img.sprite == null && !InsideControl(t, root)) { Undo.RecordObject(img, "Apply theme"); img.color = theme.control; }
                        break;
                }
            }
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>True for the parts inside a button/toggle/slider/scroll list, which their owner themes.</summary>
        private static bool InsideControl(Transform t, Transform root)
        {
            for (var p = t.parent; p != null && p != root; p = p.parent)
            {
                var kind = UiWidgets.Detect(p);
                if (kind == WidgetKind.Button || kind == WidgetKind.Toggle || kind == WidgetKind.Slider) return true;
                if (kind == WidgetKind.ScrollList && !t.IsChildOf(p.GetComponent<ScrollRect>().content)) return true;
            }
            return false;
        }

        public static void ThemeButton(Button button, UiTheme theme)
        {
            if (!button) return;
            var image = button.targetGraphic as Image;
            if (image) { Undo.RecordObject(image, "Theme"); image.color = theme.primary; }
            Undo.RecordObject(button, "Theme");
            button.colors = Tint(Color.white); // multiplies the Image colour
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                Undo.RecordObject(label, "Theme");
                label.color = theme.buttonText;
                label.fontSize = theme.fontSize;
            }
        }

        public static void ThemeToggle(Toggle toggle, UiTheme theme)
        {
            if (!toggle) return;
            if (toggle.targetGraphic) { Undo.RecordObject(toggle.targetGraphic, "Theme"); toggle.targetGraphic.color = theme.control; }
            if (toggle.graphic) { Undo.RecordObject(toggle.graphic, "Theme"); toggle.graphic.color = theme.accent; }
            Undo.RecordObject(toggle, "Theme");
            toggle.colors = Tint(Color.white);
            var label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label) { Undo.RecordObject(label, "Theme"); label.color = theme.text; label.fontSize = theme.fontSize; }
        }

        public static void ThemeSlider(Slider slider, UiTheme theme)
        {
            if (!slider) return;
            var bg = slider.transform.Find("Background")?.GetComponent<Image>();
            if (bg) { Undo.RecordObject(bg, "Theme"); bg.color = theme.control; }
            var fill = slider.fillRect ? slider.fillRect.GetComponent<Image>() : null;
            if (fill) { Undo.RecordObject(fill, "Theme"); fill.color = theme.accent; }
            var handle = slider.handleRect ? slider.handleRect.GetComponent<Image>() : null;
            if (handle) { Undo.RecordObject(handle, "Theme"); handle.color = theme.text; }
            Undo.RecordObject(slider, "Theme");
            slider.colors = Tint(Color.white);
        }

        public static void ThemeScrollList(ScrollRect sr, UiTheme theme)
        {
            if (!sr) return;
            var bg = sr.GetComponent<Image>();
            if (bg) { Undo.RecordObject(bg, "Theme"); bg.color = WithAlpha(theme.control, 0.35f); }
            if (sr.verticalScrollbar)
            {
                var track = sr.verticalScrollbar.GetComponent<Image>();
                if (track) { Undo.RecordObject(track, "Theme"); track.color = WithAlpha(theme.control, 0.6f); }
                var handle = sr.verticalScrollbar.handleRect ? sr.verticalScrollbar.handleRect.GetComponent<Image>() : null;
                if (handle) { Undo.RecordObject(handle, "Theme"); handle.color = theme.mutedText; }
            }
        }

        /// <summary>Pointer colours as multipliers of the graphic's own colour: brighter on hover, darker when pressed.</summary>
        private static ColorBlock Tint(Color normal)
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = normal;
            c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            c.selectedColor = normal;
            c.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            c.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            return c;
        }

        // ------------------------------------------------------------------ helpers

        private static LayoutElement Element(GameObject go, float minHeight, float preferredHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = preferredHeight;
            return le;
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        public static Bounds WorldBounds(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var b = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) b.Encapsulate(corners[i]);
            return b;
        }

        public static void Frame(RectTransform rt)
        {
            var view = SceneView.lastActiveSceneView;
            if (!view || !rt) return;
            var b = WorldBounds(rt);
            b.Expand(b.size.magnitude * 0.1f);
            // Look at the readable (front) side of the panel.
            view.LookAt(b.center, Quaternion.LookRotation(rt.forward, rt.up), b.size.magnitude * 0.9f);
        }
    }
}

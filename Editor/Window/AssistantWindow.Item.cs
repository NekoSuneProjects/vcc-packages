using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        private static readonly string[] AlignNames = { "Left", "Centre", "Right" };

        private void DrawItemCard()
        {
            if (!selected)
            {
                G.BeginCard("d_UnityEditor.InspectorWindow", "Edit");
                G.Hint("Click an item in the outline (or in the Scene view / Hierarchy) to edit it here.");
                G.EndCard();
                return;
            }

            var kind = UiWidgets.Detect(selected);
            G.BeginCard(UiWidgets.Icon(kind), Loc.T("Edit") + " " + Loc.T(UiWidgets.Label(kind)));

            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField(Loc.C("Name", "The object's name in the Hierarchy and the outline."), selected.name);
            G.Api("GameObject.name");
            bool visible = EditorGUILayout.Toggle(Loc.C("Visible", "Off hides it (GameObject.SetActive). Hidden items take no space."), selected.gameObject.activeSelf);
            G.Api("GameObject.SetActive()");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selected.gameObject, "Edit " + selected.name);
                selected.name = name;
                selected.gameObject.SetActive(visible);
            }
            EditorGUILayout.Space(4);

            switch (kind)
            {
                case WidgetKind.Text: DrawTextFields(selected.GetComponent<TMP_Text>()); break;
                case WidgetKind.Button: DrawButtonFields(selected.GetComponent<Button>()); DrawActionFields(selected.GetComponent<Button>()); break;
                case WidgetKind.Toggle: DrawToggleFields(selected.GetComponent<Toggle>()); DrawActionFields(selected.GetComponent<Toggle>()); break;
                case WidgetKind.Page: DrawPageFields(selected); break;
                case WidgetKind.Slider: DrawSliderFields(selected.GetComponent<Slider>()); DrawSliderActions(selected.GetComponent<Slider>()); break;
                case WidgetKind.Image: DrawImageFields(selected.GetComponent<Image>()); break;
                case WidgetKind.Divider: DrawDividerFields(selected.GetComponent<Image>()); break;
                case WidgetKind.Spacer: HeightField("Height", true); break;
                case WidgetKind.Row:
                case WidgetKind.Column: DrawGroupFields(selected.GetComponent<HorizontalOrVerticalLayoutGroup>(), kind == WidgetKind.Row); break;
                case WidgetKind.Grid: DrawGridFields(selected.GetComponent<GridLayoutGroup>()); break;
                case WidgetKind.ScrollList: DrawScrollFields(selected.GetComponent<ScrollRect>()); break;
                default: G.Hint("The Assistant doesn't have settings for this object. Use Unity's Inspector."); break;
            }

            EditorGUILayout.Space(6);
            S.Line();
            EditorGUILayout.Space(4);
            if (Help.ShowHowItWorks)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(EditorGUIUtility.IconContent("d_console.infoicon.sml"), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label("<b>" + Loc.T("How it works in Unity:") + "</b> " + UiWidgets.Explain(kind), RichHint);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Loc.C("Show in Inspector", "Select it and open Unity's Inspector to see every component."), EditorStyles.miniButton))
                {
                    Selection.activeGameObject = selected.gameObject;
                    EditorGUIUtility.PingObject(selected.gameObject);
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                }
            }
            G.EndCard();
        }

        private static GUIStyle richHint;
        private static GUIStyle RichHint => richHint ?? (richHint = new GUIStyle(S.Hint) { richText = true });

        // ------------------------------------------------------------------ per kind

        private void DrawTextFields(TMP_Text tmp)
        {
            if (!tmp) { G.Hint("This uses Unity's old Text component. The Assistant edits TextMeshPro text."); return; }
            var theme = CurrentTheme;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label(Loc.T("Text"), EditorStyles.miniBoldLabel);
            string text = EditorGUILayout.TextArea(tmp.text, Wrapped, GUILayout.MinHeight(48));
            G.Api("TextMeshProUGUI.text");
            var style = (TextStyle)EditorGUILayout.EnumPopup(Loc.C("Style", "Sets size and boldness from the theme. Title is centred."), UiWidgets.GuessStyle(tmp.fontSize, theme.fontSize));
            bool styleChanged = EditorGUI.EndChangeCheck() && style != UiWidgets.GuessStyle(tmp.fontSize, theme.fontSize);
            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider(Loc.C("Size", "Font size in UI units (mm). 26 reads well at about 2 m."), tmp.fontSize, 8, 160);
            G.Api("TextMeshProUGUI.fontSize");
            bool bold = EditorGUILayout.Toggle(Loc.T("Bold"), (tmp.fontStyle & FontStyles.Bold) != 0);
            G.Api("TextMeshProUGUI.fontStyle");
            var color = EditorGUILayout.ColorField(Loc.T("Colour"), tmp.color);
            G.Api("TextMeshProUGUI.color");
            int align = AlignIndex(tmp.alignment);
            int newAlign = EditorGUILayout.Popup(Loc.T("Line up"), align, AlignNames.Select(Loc.T).ToArray());
            G.Api("TextMeshProUGUI.alignment");
            bool changed = EditorGUI.EndChangeCheck();

            if (styleChanged || changed || tmp.text != text)
            {
                Undo.RecordObject(tmp, "Edit text");
                tmp.text = text;
                if (styleChanged) UiFactory.StyleText(tmp, style, theme);
                else
                {
                    tmp.fontSize = Mathf.Round(size);
                    tmp.fontStyle = bold ? tmp.fontStyle | FontStyles.Bold : tmp.fontStyle & ~FontStyles.Bold;
                    tmp.color = color;
                    if (newAlign != align) tmp.alignment = Align(newAlign, tmp.alignment);
                }
            }
            if (tmp.fontSize < 18) G.Report("Text under 18 is hard to read in VR from a normal distance.", false);
        }

        private void DrawButtonFields(Button button)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            var image = button.targetGraphic as Image;
            EditorGUI.BeginChangeCheck();
            string text = label ? EditorGUILayout.TextField(Loc.T("Label"), label.text) : null;
            float size = label ? EditorGUILayout.Slider(Loc.T("Label size"), label.fontSize, 8, 120) : 0;
            Color labelColor = label ? EditorGUILayout.ColorField(Loc.T("Label colour"), label.color) : default;
            Color fill = image ? EditorGUILayout.ColorField(Loc.C("Button colour", "The Image colour. Pointing at it brightens it; clicking darkens it."), image.color) : default;
            G.Api("Image.color (the Button's Target Graphic)");
            if (EditorGUI.EndChangeCheck())
            {
                if (label) { Undo.RecordObject(label, "Edit button"); label.text = text; label.fontSize = Mathf.Round(size); label.color = labelColor; }
                if (image) { Undo.RecordObject(image, "Edit button"); image.color = fill; }
            }
            HeightField("Height", false);
            ClickTargetWarning();
        }

        private void DrawToggleFields(Toggle toggle)
        {
            var label = toggle.GetComponentInChildren<TMP_Text>(true);
            EditorGUI.BeginChangeCheck();
            string text = label ? EditorGUILayout.TextField(Loc.T("Label"), label.text) : null;
            bool on = EditorGUILayout.Toggle(Loc.C("Starts on", "Toggle.isOn when the world loads."), toggle.isOn);
            G.Api("Toggle.isOn");
            Color box = toggle.targetGraphic ? EditorGUILayout.ColorField(Loc.T("Box colour"), toggle.targetGraphic.color) : default;
            G.Api("Toggle.targetGraphic → Image.color");
            Color check = toggle.graphic ? EditorGUILayout.ColorField(Loc.T("Tick colour"), toggle.graphic.color) : default;
            G.Api("Toggle.graphic → Image.color");
            if (EditorGUI.EndChangeCheck())
            {
                if (label) { Undo.RecordObject(label, "Edit toggle"); label.text = text; }
                Undo.RecordObject(toggle, "Edit toggle");
                toggle.isOn = on;
                if (toggle.graphic)
                {
                    Undo.RecordObject(toggle.graphic, "Edit toggle");
                    toggle.graphic.color = check;
                    toggle.graphic.canvasRenderer.SetAlpha(on ? 1f : 0f);
                }
                if (toggle.targetGraphic) { Undo.RecordObject(toggle.targetGraphic, "Edit toggle"); toggle.targetGraphic.color = box; }
            }
            HeightField("Height", false);
        }

        private void DrawSliderFields(Slider slider)
        {
            var bg = slider.transform.Find("Background")?.GetComponent<Image>();
            var fill = slider.fillRect ? slider.fillRect.GetComponent<Image>() : null;
            var handle = slider.handleRect ? slider.handleRect.GetComponent<Image>() : null;
            EditorGUI.BeginChangeCheck();
            float min, max;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(Loc.C("From / to", "Slider.minValue and maxValue."));
                min = EditorGUILayout.FloatField(slider.minValue);
                max = EditorGUILayout.FloatField(slider.maxValue);
            }
            float value = EditorGUILayout.Slider(Loc.C("Starts at"), slider.value, Mathf.Min(min, max), Mathf.Max(min, max));
            G.Api("Slider.minValue / maxValue / value");
            bool whole = EditorGUILayout.Toggle(Loc.C("Whole numbers only", "e.g. 1, 2, 3 instead of 1.37"), slider.wholeNumbers);
            G.Api("Slider.wholeNumbers");
            Color track = bg ? EditorGUILayout.ColorField(Loc.T("Track colour"), bg.color) : default;
            Color fillC = fill ? EditorGUILayout.ColorField(Loc.T("Fill colour"), fill.color) : default;
            Color handleC = handle ? EditorGUILayout.ColorField(Loc.T("Handle colour"), handle.color) : default;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(slider, "Edit slider");
                slider.minValue = min;
                slider.maxValue = max;
                slider.wholeNumbers = whole;
                slider.value = value;
                if (bg) { Undo.RecordObject(bg, "Edit slider"); bg.color = track; }
                if (fill) { Undo.RecordObject(fill, "Edit slider"); fill.color = fillC; }
                if (handle) { Undo.RecordObject(handle, "Edit slider"); handle.color = handleC; }
            }
            HeightField("Height", false);
        }

        private void DrawImageFields(Image image)
        {
            EditorGUI.BeginChangeCheck();
            var sprite = (Sprite)EditorGUILayout.ObjectField(Loc.C("Picture", "A texture imported with Texture Type = Sprite (2D and UI)."), image.sprite, typeof(Sprite), false);
            G.Api("Image.sprite");
            var color = EditorGUILayout.ColorField(Loc.C("Tint", "Multiplies the picture's colours. White = unchanged."), image.color);
            G.Api("Image.color");
            bool keep = EditorGUILayout.Toggle(Loc.C("Keep shape", "Don't stretch the picture (Image.preserveAspect)."), image.preserveAspect);
            G.Api("Image.preserveAspect");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(image, "Edit image");
                if (sprite != image.sprite && sprite && image.sprite == null && image.color == CurrentTheme.control) color = Color.white;
                image.sprite = sprite;
                image.color = color;
                image.preserveAspect = keep;
            }
            HeightField("Height", true);
        }

        private void DrawDividerFields(Image image)
        {
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(Loc.T("Colour"), image.color);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(image, "Edit divider"); image.color = color; }
            HeightField("Thickness", true, 1f, 8f);
        }

        private void DrawGroupFields(HorizontalOrVerticalLayoutGroup g, bool row)
        {
            EditorGUI.BeginChangeCheck();
            float spacing = EditorGUILayout.Slider(Loc.T("Gap between items"), g.spacing, 0, 80);
            G.Api("HorizontalOrVerticalLayoutGroup.spacing");
            bool stretch = EditorGUILayout.Toggle(new GUIContent(row ? "Share the width equally" : "Stretch to fill", "childForceExpand: items grow to fill the space."), row ? g.childForceExpandWidth : g.childForceExpandHeight);
            var align = (TextAnchor)EditorGUILayout.EnumPopup(Loc.C("Line up items", "childAlignment"), g.childAlignment);
            G.Api("LayoutGroup.childAlignment / childForceExpand");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(g, "Edit layout");
                g.spacing = spacing;
                if (row) g.childForceExpandWidth = stretch; else g.childForceExpandHeight = stretch;
                g.childAlignment = align;
            }
            G.Hint("While this is selected, new items from the Add card go inside it.");
        }

        private void DrawGridFields(GridLayoutGroup g)
        {
            EditorGUI.BeginChangeCheck();
            int cols = EditorGUILayout.IntSlider(Loc.C("Columns"), Mathf.Max(1, g.constraintCount), 1, 8);
            G.Api("GridLayoutGroup.constraintCount");
            float w = EditorGUILayout.Slider(Loc.T("Cell width"), g.cellSize.x, 40, 800);
            float h = EditorGUILayout.Slider(Loc.T("Cell height"), g.cellSize.y, 30, 600);
            float gap = EditorGUILayout.Slider(Loc.T("Gap"), g.spacing.x, 0, 80);
            G.Api("GridLayoutGroup.cellSize / spacing");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(g, "Edit grid");
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = cols;
                g.cellSize = new Vector2(w, h);
                g.spacing = new Vector2(gap, gap);
            }
            var panelWidth = ((RectTransform)panel.transform).sizeDelta.x;
            float needed = cols * w + (cols - 1) * gap;
            if (needed > panelWidth) G.Report("The grid is " + Mathf.RoundToInt(needed) + " wide but the panel is " + Mathf.RoundToInt(panelWidth) + ". Use fewer columns or smaller cells.", false);
            G.Hint("While this is selected, new items from the Add card go inside it.");
        }

        private void DrawScrollFields(ScrollRect sr)
        {
            HeightField("Height", true);
            var v = sr.content ? sr.content.GetComponent<VerticalLayoutGroup>() : null;
            if (v)
            {
                EditorGUI.BeginChangeCheck();
                float spacing = EditorGUILayout.Slider(Loc.T("Gap between items"), v.spacing, 0, 80);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(v, "Edit list"); v.spacing = spacing; }
            }
            G.Hint("While this is selected, new items from the Add card go inside the list. Players scroll it by dragging with the laser.");
        }

        // ------------------------------------------------------------------ shared fields

        /// <summary>Height via LayoutElement: preferred height for content-like items, minimum height for controls.</summary>
        private void HeightField(string label, bool preferred, float min = 20f, float max = 1200f)
        {
            var le = selected.GetComponent<LayoutElement>();
            float current = le ? (preferred ? le.preferredHeight : le.minHeight) : -1;
            if (current < 0) current = ((RectTransform)selected).rect.height;
            EditorGUI.BeginChangeCheck();
            float h = EditorGUILayout.Slider(new GUIContent(label, "LayoutElement " + (preferred ? "Preferred" : "Min") + " Height, in mm."), current, min, max);
            G.Api("LayoutElement.preferredHeight / minHeight");
            if (!EditorGUI.EndChangeCheck()) return;
            if (!le) le = Undo.AddComponent<LayoutElement>(selected.gameObject);
            Undo.RecordObject(le, "Edit height");
            if (preferred) { le.preferredHeight = h; if (le.minHeight > h) le.minHeight = h; }
            else le.minHeight = h;
        }

        private void ClickTargetWarning()
        {
            var r = ((RectTransform)selected).rect;
            if (r.height > 0 && (r.height < 40 || r.width < 60))
                G.Report("Small targets are hard to hit with the VRChat laser. Aim for at least 60 × 40.", false);
        }

        private static GUIStyle wrapped;
        private static GUIStyle Wrapped => wrapped ?? (wrapped = new GUIStyle(EditorStyles.textArea) { wordWrap = true });

        private static int AlignIndex(TextAlignmentOptions a)
        {
            int h = (int)a & 0xFF;
            if (h == (int)HorizontalAlignmentOptions.Center) return 1;
            if (h == (int)HorizontalAlignmentOptions.Right) return 2;
            return 0;
        }

        private static TextAlignmentOptions Align(int index, TextAlignmentOptions current)
        {
            int vertical = (int)current & 0xFF00;
            int h = index == 1 ? (int)HorizontalAlignmentOptions.Center : index == 2 ? (int)HorizontalAlignmentOptions.Right : (int)HorizontalAlignmentOptions.Left;
            return (TextAlignmentOptions)(vertical | h);
        }
    }
}

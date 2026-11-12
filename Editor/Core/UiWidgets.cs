using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NekoSune.WorldUiAssistant
{
    internal enum WidgetKind { Panel, Text, Button, Toggle, Slider, Image, Divider, Spacer, Row, Column, Grid, ScrollList, Other }

    internal enum TextStyle { Title, Heading, Body, Caption }

    /// <summary>
    /// Works out what a UI object is from its ordinary Unity components, so the Assistant also
    /// understands UI built by hand. Nothing extra is stored on the objects.
    /// </summary>
    internal static class UiWidgets
    {
        public static WidgetKind Detect(Transform t)
        {
            if (t == null) return WidgetKind.Other;
            if (t.GetComponent<Canvas>() && t.GetComponent<Canvas>().isRootCanvas) return WidgetKind.Panel;
            if (t.GetComponent<Button>()) return WidgetKind.Button;
            if (t.GetComponent<Toggle>()) return WidgetKind.Toggle;
            if (t.GetComponent<Slider>()) return WidgetKind.Slider;
            if (t.GetComponent<ScrollRect>()) return WidgetKind.ScrollList;
            if (t.GetComponent<GridLayoutGroup>()) return WidgetKind.Grid;
            if (t.GetComponent<HorizontalLayoutGroup>()) return WidgetKind.Row;
            if (t.GetComponent<VerticalLayoutGroup>()) return WidgetKind.Column;
            if (t.GetComponent<TMP_Text>() || t.GetComponent<Text>()) return WidgetKind.Text;
            var image = t.GetComponent<Image>();
            var le = t.GetComponent<LayoutElement>();
            if (image) return le && le.preferredHeight > 0 && le.preferredHeight <= 8 ? WidgetKind.Divider : WidgetKind.Image;
            if (le && !t.GetComponent<Graphic>() && t.childCount == 0) return WidgetKind.Spacer;
            return WidgetKind.Other;
        }

        /// <summary>Where children of this object live (a scroll list keeps them in its Content).</summary>
        public static Transform ContentOf(Transform t)
        {
            switch (Detect(t))
            {
                case WidgetKind.Panel:
                case WidgetKind.Row:
                case WidgetKind.Column:
                case WidgetKind.Grid:
                    return t;
                case WidgetKind.ScrollList:
                    var sr = t.GetComponent<ScrollRect>();
                    return sr.content ? sr.content : null;
                default:
                    return null;
            }
        }

        public static bool IsContainer(Transform t) => ContentOf(t) != null;

        public static string Label(WidgetKind kind)
        {
            switch (kind)
            {
                case WidgetKind.ScrollList: return "Scroll list";
                case WidgetKind.Other: return "Object";
                default: return kind.ToString();
            }
        }

        public static string Icon(WidgetKind kind)
        {
            switch (kind)
            {
                case WidgetKind.Panel: return "Canvas Icon";
                case WidgetKind.Text: return "Text Icon";
                case WidgetKind.Button: return "Button Icon";
                case WidgetKind.Toggle: return "Toggle Icon";
                case WidgetKind.Slider: return "Slider Icon";
                case WidgetKind.Image: return "Image Icon";
                case WidgetKind.Divider: return "d_Toolbar Minus";
                case WidgetKind.Spacer: return "RectTransform Icon";
                case WidgetKind.Row: return "HorizontalLayoutGroup Icon";
                case WidgetKind.Column: return "VerticalLayoutGroup Icon";
                case WidgetKind.Grid: return "GridLayoutGroup Icon";
                case WidgetKind.ScrollList: return "ScrollRect Icon";
                default: return "GameObject Icon";
            }
        }

        /// <summary>One plain-English line on what Unity components make up this widget.</summary>
        public static string Explain(WidgetKind kind)
        {
            switch (kind)
            {
                case WidgetKind.Panel: return "A Canvas set to World Space (it exists in the 3D world), a GraphicRaycaster so the laser can click it, VRChat's VRCUiShape, a background Image and a VerticalLayoutGroup that stacks everything top to bottom.";
                case WidgetKind.Text: return "A TextMeshPro - Text (UI) component. Its height comes from the text, because the layout group asks it for its preferred size.";
                case WidgetKind.Button: return "An Image (the button background) plus a Button component that changes colour when pointed at, and a TextMeshPro child for the label.";
                case WidgetKind.Toggle: return "A Toggle component. Its Graphic (the check) is shown when it is on; the Box is its Target Graphic that reacts to the pointer.";
                case WidgetKind.Slider: return "A Slider component that moves the Handle and stretches the Fill between Min and Max.";
                case WidgetKind.Image: return "An Image component. Give it a Sprite (a texture imported as Sprite (2D and UI)).";
                case WidgetKind.Divider: return "A thin Image. A LayoutElement fixes its height.";
                case WidgetKind.Spacer: return "An empty object with a LayoutElement. It only takes up space.";
                case WidgetKind.Row: return "A HorizontalLayoutGroup: places its children side by side.";
                case WidgetKind.Column: return "A VerticalLayoutGroup: stacks its children top to bottom.";
                case WidgetKind.Grid: return "A GridLayoutGroup: places children in equal cells, row by row.";
                case WidgetKind.ScrollList: return "A ScrollRect with a masked Viewport. Items go in Content, which grows with a ContentSizeFitter.";
                default: return "Something the Assistant doesn't edit. Use the Inspector.";
            }
        }

        public static float StyleScale(TextStyle style)
        {
            switch (style)
            {
                case TextStyle.Title: return 1.6f;
                case TextStyle.Heading: return 1.25f;
                case TextStyle.Caption: return 0.75f;
                default: return 1f;
            }
        }

        /// <summary>Closest style for a font size, relative to the theme's base size.</summary>
        public static TextStyle GuessStyle(float fontSize, int baseSize)
        {
            float r = fontSize / Mathf.Max(1, baseSize);
            if (r >= 1.45f) return TextStyle.Title;
            if (r >= 1.12f) return TextStyle.Heading;
            if (r <= 0.85f) return TextStyle.Caption;
            return TextStyle.Body;
        }
    }
}

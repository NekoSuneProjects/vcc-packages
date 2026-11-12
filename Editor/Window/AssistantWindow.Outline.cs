using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        private const string DragKey = "NekoSune.WorldUiAssistant.OutlineItem";

        private static readonly (WidgetKind kind, string label, string tip, bool needsText)[] Palette =
        {
            (WidgetKind.Panel, "Title", "Big bold heading text.", true),
            (WidgetKind.Text, "Text", "A paragraph of text.", true),
            (WidgetKind.Button, "Button", "Something to click. Give it an action in its Edit card.", true),
            (WidgetKind.Toggle, "Toggle", "An on/off checkbox with a label.", true),
            (WidgetKind.Slider, "Slider", "Drag to pick a value, e.g. volume.", false),
            (WidgetKind.Image, "Image", "A picture. Pick a Sprite after adding it.", false),
            (WidgetKind.Divider, "Divider", "A thin line between sections.", false),
            (WidgetKind.Spacer, "Spacer", "Empty space.", false),
            (WidgetKind.Row, "Row", "Puts the items inside it side by side.", false),
            (WidgetKind.Grid, "Grid", "Equal cells, e.g. a grid of buttons.", false),
            (WidgetKind.ScrollList, "Scroll list", "A box you can scroll when it has more than fits.", false),
            (WidgetKind.Page, "Page", "A new page with a Back button. The first time, everything on the panel becomes the Home page.", false),
        };

        private readonly HashSet<int> collapsed = new HashSet<int>();
        private Transform dragCandidate;
        private Vector2 dragStart;
        private Rect dropLine;
        private Rect dropBox;

        // ------------------------------------------------------------------ palette

        /// <summary>Where a new widget goes: inside the selected container, after the selected item, or at the end.</summary>
        private (Transform parent, int index, string where) AddTarget()
        {
            if (selected && selected != panel.transform)
            {
                var content = UiWidgets.ContentOf(selected);
                if (content) return (content, -1, "inside " + selected.name);
                return (selected.parent, selected.GetSiblingIndex() + 1, "after " + selected.name);
            }
            var shownPage = UiWidgets.PagesOf(panel.transform).FirstOrDefault(p => p.gameObject.activeSelf);
            if (shownPage) return (shownPage, -1, "at the bottom of the page " + shownPage.name);
            return (panel.transform, -1, "at the bottom of the panel");
        }

        private void DrawPalette()
        {
            var target = AddTarget();
            G.BeginCard("Toolbar Plus", "Add");
            int cols = Mathf.Clamp(Mathf.FloorToInt((position.width - 40f) / 120f), 2, 6);
            bool fontOk = UiFactory.HasTmpFont;
            for (int i = 0; i < Palette.Length; i += cols)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < i + cols; j++)
                    {
                        if (j >= Palette.Length) { GUILayout.Label(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.MinWidth(10)); continue; }
                        var p = Palette[j];
                        string icon = p.kind == WidgetKind.Panel ? "Text Icon" : UiWidgets.Icon(p.kind);
                        using (new EditorGUI.DisabledScope(p.needsText && !fontOk))
                        {
                            if (GUILayout.Button(new GUIContent(" " + Loc.T(p.label), EditorGUIUtility.IconContent(icon).image, Loc.T(p.tip)), GUILayout.Height(26), GUILayout.MinWidth(10)))
                                Add(p.kind, target);
                        }
                    }
                }
            }
            EditorGUILayout.Space(2);
            G.Hint("New items go " + target.where + ". Select something in the outline to add after it, or select a Row, Grid or Scroll list to add inside it.");
            G.EndCard();
        }

        private void Add(WidgetKind kind, (Transform parent, int index, string where) target)
        {
            var theme = CurrentTheme;
            if (kind == WidgetKind.Page)
            {
                Select(UiActions.AddPage(panel, theme, UiFactory.HasTmpFont));
                return;
            }
            var go = kind == WidgetKind.Panel
                ? UiFactory.CreateTitle(target.parent, target.index, theme)
                : UiFactory.Create(kind, target.parent, target.index, theme);
            if (kind == WidgetKind.Row || kind == WidgetKind.Grid)
            {
                // A row or grid on its own is invisible, so start it with two buttons.
                for (int i = 0; i < 2; i++)
                    if (UiFactory.HasTmpFont) UiFactory.Create(WidgetKind.Button, go.transform, -1, theme);
            }
            Select(go.transform);
        }

        // ------------------------------------------------------------------ outline

        /// <summary>The outline row an object belongs to (e.g. a button's label → the button).</summary>
        private Transform OutlineItemFor(Transform t)
        {
            if (!panel || !t || !t.IsChildOf(panel.transform)) return null;
            if (t == panel.transform) return null;
            Transform best = null;
            for (var x = t; x != null && x != panel.transform; x = x.parent)
            {
                var parent = x.parent;
                if (parent == null) break;
                bool parentIsContent = parent == panel.transform || IsContentOfContainer(parent);
                if (parentIsContent) { best = x; break; }
            }
            return best;
        }

        private static bool IsContentOfContainer(Transform t)
        {
            if (UiWidgets.IsContainer(t) && UiWidgets.ContentOf(t) == t && UiWidgets.Detect(t) != WidgetKind.Panel) return true;
            // A scroll list's Content object.
            var sr = t.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
            return sr && sr.content == t;
        }

        private void DrawOutline()
        {
            G.BeginCard("UnityEditor.SceneHierarchyWindow", "Outline", "Top to bottom, like the panel. Drag to reorder, drop on a Row, Grid or Scroll list to put it inside.");
            if (panel.transform.childCount == 0) G.Hint("Empty. Add something from the Add card.");
            DrawItems(panel.transform, 0);

            if (Event.current.type == EventType.Repaint)
            {
                if (dropLine.width > 0) EditorGUI.DrawRect(dropLine, S.Accent);
                if (dropBox.width > 0)
                {
                    EditorGUI.DrawRect(new Rect(dropBox.x, dropBox.y, dropBox.width, 2), S.Accent);
                    EditorGUI.DrawRect(new Rect(dropBox.x, dropBox.yMax - 2, dropBox.width, 2), S.Accent);
                    EditorGUI.DrawRect(new Rect(dropBox.x, dropBox.y, 2, dropBox.height), S.Accent);
                    EditorGUI.DrawRect(new Rect(dropBox.xMax - 2, dropBox.y, 2, dropBox.height), S.Accent);
                }
            }
            if (Event.current.type == EventType.DragExited) ClearDrop();
            G.EndCard();
        }

        private void DrawItems(Transform content, int depth)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                var t = content.GetChild(i);
                var kind = UiWidgets.Detect(t);
                var inner = UiWidgets.ContentOf(t);
                bool open = inner && !collapsed.Contains(t.GetInstanceID());
                DrawRow(t, kind, depth, inner != null, open);
                if (open) DrawItems(inner, depth + 1);
            }
        }

        private void DrawRow(Transform t, WidgetKind kind, int depth, bool container, bool open)
        {
            var row = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            bool isSelected = selected == t;
            var e = Event.current;

            // Buttons on the right.
            var del = new Rect(row.xMax - 22, row.y + 2, 22, 18);
            var dup = new Rect(del.x - 22, row.y + 2, 22, 18);
            var eye = new Rect(dup.x - 24, row.y + 2, 22, 18);

            if (e.type == EventType.Repaint)
            {
                if (isSelected) EditorGUI.DrawRect(row, UiFactory.WithAlpha(S.Accent, 0.35f));
                else if (row.Contains(e.mousePosition)) EditorGUI.DrawRect(row, UiFactory.WithAlpha(S.CardBorder, 0.5f));
            }

            float x = row.x + 4 + depth * 16;
            if (container)
            {
                var arrow = new Rect(x, row.y + 3, 14, 16);
                if (GUI.Button(arrow, EditorGUIUtility.IconContent(open ? "IN foldout on" : "IN foldout"), GUIStyle.none))
                {
                    if (open) collapsed.Add(t.GetInstanceID()); else collapsed.Remove(t.GetInstanceID());
                }
            }
            x += 16;
            var iconTex = EditorGUIUtility.IconContent(UiWidgets.Icon(kind)).image;
            if (iconTex && e.type == EventType.Repaint) GUI.DrawTexture(new Rect(x, row.y + 3, 16, 16), iconTex, ScaleMode.ScaleToFit);
            x += 20;

            var kindLabel = new GUIContent(Loc.T(UiWidgets.Label(kind)));
            float kindW = S.Chip.CalcSize(kindLabel).x;
            var nameStyle = t.gameObject.activeSelf ? EditorStyles.label : S.PathLabel;
            var nameRect = new Rect(x, row.y + 2, Mathf.Max(20, eye.x - x - kindW - 8), 18);
            GUI.Label(nameRect, t.name, nameStyle);
            if (e.type == EventType.Repaint)
                S.Chip.Draw(new Rect(eye.x - kindW - 4, row.y + 3, kindW, 16), kindLabel, false, false, false, false);

            if (GUI.Button(eye, new GUIContent(EditorGUIUtility.IconContent(t.gameObject.activeSelf ? "d_scenevis_visible_hover" : "d_scenevis_hidden_hover").image, Loc.T("Show / hide")), GUIStyle.none))
            {
                if (kind == WidgetKind.Page) UiActions.ShowPage(t);
                else
                {
                    Undo.RecordObject(t.gameObject, "Show / hide");
                    t.gameObject.SetActive(!t.gameObject.activeSelf);
                }
            }
            if (GUI.Button(dup, new GUIContent(EditorGUIUtility.IconContent("d_TreeEditor.Duplicate").image, Loc.T("Duplicate")), GUIStyle.none))
            {
                var copy = Object.Instantiate(t.gameObject, t.parent);
                copy.name = t.name;
                copy.transform.SetSiblingIndex(t.GetSiblingIndex() + 1);
                Undo.RegisterCreatedObjectUndo(copy, "Duplicate " + t.name);
                Select(copy.transform);
                GUIUtility.ExitGUI();
            }
            if (GUI.Button(del, new GUIContent(EditorGUIUtility.IconContent("d_TreeEditor.Trash").image, Loc.T("Delete")), GUIStyle.none))
            {
                if (selected == t || (selected && selected.IsChildOf(t))) selected = null;
                Undo.DestroyObjectImmediate(t.gameObject);
                if (kind == WidgetKind.Page) UiActions.RefreshAllPages(panel);
                GUIUtility.ExitGUI();
            }

            HandleRowMouse(row, t, container);
        }

        private void HandleRowMouse(Rect row, Transform t, bool container)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && row.Contains(e.mousePosition))
                    {
                        SelectFromOutline(t);
                        dragCandidate = t;
                        dragStart = e.mousePosition;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (dragCandidate == t && (e.mousePosition - dragStart).magnitude > 5f)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.SetGenericData(DragKey, t);
                        DragAndDrop.objectReferences = new Object[] { t.gameObject };
                        DragAndDrop.StartDrag(t.name);
                        dragCandidate = null;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    dragCandidate = null;
                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!row.Contains(e.mousePosition)) break;
                    var dragged = DragAndDrop.GetGenericData(DragKey) as Transform;
                    if (!dragged) break;
                    if (dragged == t || t.IsChildOf(dragged))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                        ClearDrop();
                        e.Use();
                        break;
                    }
                    float rel = (e.mousePosition.y - row.y) / row.height;
                    bool inside = container && rel > 0.3f && rel < 0.7f;
                    bool after = !inside && rel >= 0.5f;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    if (e.type == EventType.DragUpdated)
                    {
                        ClearDrop();
                        if (inside) dropBox = row;
                        else dropLine = new Rect(row.x, after ? row.yMax - 1 : row.y - 1, row.width, 2);
                        Repaint();
                    }
                    else
                    {
                        DragAndDrop.AcceptDrag();
                        ClearDrop();
                        Move(dragged, t, inside, after);
                    }
                    e.Use();
                    break;
            }
        }

        private void ClearDrop()
        {
            dropLine = default;
            dropBox = default;
        }

        private void Move(Transform item, Transform target, bool inside, bool after)
        {
            Transform parent;
            int index;
            if (inside)
            {
                parent = UiWidgets.ContentOf(target);
                index = parent.childCount;
                collapsed.Remove(target.GetInstanceID());
            }
            else
            {
                parent = target.parent;
                index = target.GetSiblingIndex() + (after ? 1 : 0);
                if (item.parent == parent && item.GetSiblingIndex() < index) index--;
            }
            Undo.SetTransformParent(item, parent, "Move " + item.name);
            Undo.RegisterChildrenOrderUndo(parent, "Move " + item.name);
            item.SetSiblingIndex(Mathf.Clamp(index, 0, parent.childCount - 1));
            Select(item);
            GUIUtility.ExitGUI();
        }
    }
}

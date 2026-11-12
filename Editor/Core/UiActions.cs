using System.Collections.Generic;
using System.Linq;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.Udon;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>What a button or toggle can do, in the order shown in the action picker.</summary>
    internal enum ActionChoice { Nothing, Show, Hide, ShowHide, Mirror, PlaySound, StopSound, Teleport, OpenPage, CustomEvent, ShowOne }

    /// <summary>What a slider can drive.</summary>
    internal enum SliderChoice { Nothing, SoundVolume, AnimatorFloat, SendToUdon }

    /// <summary>
    /// Adds, edits and removes the NekoUiAction Udon script behind a button or toggle, and wires the
    /// UI event to it: onClick / onValueChanged → UdonBehaviour.SendCustomEvent("Run").
    /// </summary>
    internal static class UiActions
    {
        public const string RunEvent = "Run";

        public static readonly string[] ChoiceLabels =
        {
            "Nothing yet", "Show something", "Hide something", "Show / hide something", "Turn a mirror on / off",
            "Play a sound", "Stop a sound", "Teleport the player", "Open a page", "Send an Udon event", "Show one of a group",
        };

        public static NekoUiAction Get(Selectable s) => s ? s.GetComponent<NekoUiAction>() : null;

        public static ActionChoice ChoiceOf(NekoUiAction a)
        {
            if (!a) return ActionChoice.Nothing;
            switch (a.action)
            {
                case NekoUiAction.ShowObjects: return ActionChoice.Show;
                case NekoUiAction.HideObjects: return ActionChoice.Hide;
                case NekoUiAction.ToggleObjects: return a.targets != null && a.targets.Length > 0 && IsMirror(a.targets[0]) ? ActionChoice.Mirror : ActionChoice.ShowHide;
                case NekoUiAction.PlaySound: return ActionChoice.PlaySound;
                case NekoUiAction.StopSound: return ActionChoice.StopSound;
                case NekoUiAction.TeleportPlayer: return ActionChoice.Teleport;
                case NekoUiAction.OpenPage: return ActionChoice.OpenPage;
                case NekoUiAction.CustomEvent: return ActionChoice.CustomEvent;
                case NekoUiAction.ShowOneOfGroup: return ActionChoice.ShowOne;
                default: return ActionChoice.Nothing;
            }
        }

        private static int ActionNumber(ActionChoice c)
        {
            switch (c)
            {
                case ActionChoice.Show: return NekoUiAction.ShowObjects;
                case ActionChoice.Hide: return NekoUiAction.HideObjects;
                case ActionChoice.PlaySound: return NekoUiAction.PlaySound;
                case ActionChoice.StopSound: return NekoUiAction.StopSound;
                case ActionChoice.Teleport: return NekoUiAction.TeleportPlayer;
                case ActionChoice.OpenPage: return NekoUiAction.OpenPage;
                case ActionChoice.CustomEvent: return NekoUiAction.CustomEvent;
                case ActionChoice.ShowOne: return NekoUiAction.ShowOneOfGroup;
                default: return NekoUiAction.ToggleObjects; // ShowHide, Mirror
            }
        }

        /// <summary>Teleports and pages only ever affect the player who clicked.</summary>
        public static bool CanSync(ActionChoice c) => c != ActionChoice.Teleport && c != ActionChoice.OpenPage && c != ActionChoice.Nothing;

        // ------------------------------------------------------------------ add / change / remove

        /// <summary>Sets what the control does (one Undo step). Nothing removes the script and its wiring.</summary>
        public static NekoUiAction SetChoice(Selectable s, ActionChoice choice)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var a = Get(s);
            if (choice == ActionChoice.Nothing)
            {
                if (a)
                {
                    Unwire(s, a);
                    UdonSharpUndo.DestroyImmediate(a);
                }
                Undo.CollapseUndoOperations(group);
                return null;
            }

            if (!a) a = UdonSharpUndo.AddComponent<NekoUiAction>(s.gameObject);
            Edit(a, "Set action", x =>
            {
                x.action = ActionNumber(choice);
                x.uiToggle = s as Toggle;
                if (!CanSync(choice)) x.synced = false;
                if (choice == ActionChoice.Mirror && (x.targets == null || x.targets.Length == 0 || !IsMirror(x.targets[0])))
                {
                    var mirror = FindMirrors().FirstOrDefault();
                    x.targets = mirror ? new[] { mirror } : new GameObject[0];
                }
                if (choice == ActionChoice.OpenPage) RefreshPages(x);
            });
            Wire(s, a);
            Undo.CollapseUndoOperations(group);
            return a;
        }

        /// <summary>Records, changes and pushes the values to the Udon side.</summary>
        public static void Edit(NekoUiAction a, string undoName, System.Action<NekoUiAction> change)
        {
            Undo.RecordObject(a, undoName);
            change(a);
            UdonSharpEditorUtility.CopyProxyToUdon(a);
            EditorUtility.SetDirty(a);
        }

        private static UnityEventBase EventOf(Selectable s)
        {
            if (s is Button b) return b.onClick;
            if (s is Toggle t) return t.onValueChanged;
            return null;
        }

        public static bool IsWired(Selectable s, NekoUiAction a)
        {
            var evt = EventOf(s);
            var udon = a ? UdonSharpEditorUtility.GetBackingUdonBehaviour(a) : null;
            if (evt == null || !udon) return false;
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                if (evt.GetPersistentTarget(i) == udon && evt.GetPersistentMethodName(i) == nameof(UdonBehaviour.SendCustomEvent))
                    return true;
            return false;
        }

        public static void Wire(Selectable s, NekoUiAction a)
        {
            if (IsWired(s, a)) return;
            var evt = EventOf(s);
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(a);
            if (evt == null || !udon) return;
            Undo.RecordObject(s, "Wire action");
            UnityEventTools.AddStringPersistentListener(evt, udon.SendCustomEvent, RunEvent);
            EditorUtility.SetDirty(s);
        }

        private static void Unwire(Selectable s, NekoUiAction a)
        {
            var evt = EventOf(s);
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(a);
            if (evt == null || !udon) return;
            Undo.RecordObject(s, "Remove action");
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                if (evt.GetPersistentTarget(i) == udon) UnityEventTools.RemovePersistentListener(evt, i);
            EditorUtility.SetDirty(s);
        }

        // ------------------------------------------------------------------ sliders

        public static readonly string[] SliderLabels = { "Nothing yet", "Change sound volume", "Set an Animator value", "Send the value to Udon" };

        public static NekoUiSlider GetSlider(Slider s) => s ? s.GetComponent<NekoUiSlider>() : null;

        public static SliderChoice SliderChoiceOf(NekoUiSlider a) => a ? (SliderChoice)(a.action + 1) : SliderChoice.Nothing;

        public static NekoUiSlider SetSliderChoice(Slider s, SliderChoice choice)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var a = GetSlider(s);
            var evt = s.onValueChanged;
            if (choice == SliderChoice.Nothing)
            {
                if (a)
                {
                    var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(a);
                    Undo.RecordObject(s, "Remove action");
                    for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                        if (evt.GetPersistentTarget(i) == udon) UnityEventTools.RemovePersistentListener(evt, i);
                    UdonSharpUndo.DestroyImmediate(a);
                }
                Undo.CollapseUndoOperations(group);
                return null;
            }
            if (!a) a = UdonSharpUndo.AddComponent<NekoUiSlider>(s.gameObject);
            Undo.RecordObject(a, "Slider action");
            a.action = (int)choice - 1;
            a.slider = s;
            UdonSharpEditorUtility.CopyProxyToUdon(a);
            WireSlider(s, a);
            Undo.CollapseUndoOperations(group);
            return a;
        }

        public static bool IsSliderWired(Slider s, NekoUiSlider a)
        {
            var udon = a ? UdonSharpEditorUtility.GetBackingUdonBehaviour(a) : null;
            if (!udon) return false;
            for (int i = 0; i < s.onValueChanged.GetPersistentEventCount(); i++)
                if (s.onValueChanged.GetPersistentTarget(i) == udon) return true;
            return false;
        }

        public static void WireSlider(Slider s, NekoUiSlider a)
        {
            if (IsSliderWired(s, a)) return;
            var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(a);
            Undo.RecordObject(s, "Wire slider");
            UnityEventTools.AddStringPersistentListener(s.onValueChanged, udon.SendCustomEvent, "Changed");
            EditorUtility.SetDirty(s);
        }

        public static void EditSlider(NekoUiSlider a, string undoName, System.Action<NekoUiSlider> change)
        {
            Undo.RecordObject(a, undoName);
            change(a);
            UdonSharpEditorUtility.CopyProxyToUdon(a);
            EditorUtility.SetDirty(a);
        }

        public static List<NekoToast> FindToasts() =>
            Resources.FindObjectsOfTypeAll<NekoToast>().Where(t => t && !EditorUtility.IsPersistent(t) && t.gameObject.scene.IsValid()).ToList();

        // ------------------------------------------------------------------ helpers

        private static System.Type mirrorType;
        public static bool IsMirror(GameObject go)
        {
            if (!go) return false;
            if (mirrorType == null) mirrorType = TypeCache.GetTypesDerivedFrom<Component>().FirstOrDefault(t => t.Name == "VRCMirrorReflection");
            return mirrorType != null && go.GetComponentInChildren(mirrorType, true);
        }

        public static List<GameObject> FindMirrors()
        {
            if (mirrorType == null) IsMirror(null);
            if (mirrorType == null) return new List<GameObject>();
            return Resources.FindObjectsOfTypeAll(mirrorType)
                .OfType<Component>()
                .Where(c => c && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid())
                .Select(c => c.gameObject)
                .Distinct()
                .ToList();
        }

        /// <summary>An Open-a-page action hides the other pages of its menu, so it keeps a list of them.</summary>
        public static void RefreshPages(NekoUiAction a)
        {
            if (!a.page) { a.pages = new GameObject[0]; return; }
            a.pages = UiWidgets.PagesOf(a.page.transform.parent).Select(t => t.gameObject).ToArray();
        }

        /// <summary>Updates every Open-a-page action in a panel after pages were added or removed.</summary>
        public static void RefreshAllPages(Canvas panel)
        {
            foreach (var a in panel.GetComponentsInChildren<NekoUiAction>(true))
                if (a.action == NekoUiAction.OpenPage)
                {
                    var before = a.pages ?? new GameObject[0];
                    var pages = a.page ? UiWidgets.PagesOf(a.page.transform.parent).Select(t => t.gameObject).ToArray() : new GameObject[0];
                    if (!before.SequenceEqual(pages)) Edit(a, "Update pages", x => x.pages = pages);
                }
        }

        /// <summary>Shows one page and hides the others (in the editor, so you can work on it).</summary>
        public static void ShowPage(Transform page)
        {
            foreach (var p in UiWidgets.PagesOf(page.parent))
            {
                if (p.gameObject.activeSelf == (p == page)) continue;
                Undo.RecordObject(p.gameObject, "Show page");
                p.gameObject.SetActive(p == page);
            }
        }

        // ------------------------------------------------------------------ pages

        /// <summary>
        /// Adds a page with a heading and a Back button. The first time, everything already on the
        /// panel is moved into a "Home" page. The new page is shown so you can fill it.
        /// </summary>
        public static Transform AddPage(Canvas panel, UiTheme theme, bool hasFont)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var root = panel.transform;
            var pages = UiWidgets.PagesOf(root);

            if (pages.Count == 0)
            {
                var existing = root.Cast<Transform>().ToList();
                var home = NewPage("Home", root, theme);
                foreach (var child in existing) Undo.SetTransformParent(child, home, "Move into Home page");
                pages.Add(home);
            }

            var page = NewPage("Page " + (pages.Count + 1), root, theme);
            if (hasFont)
            {
                var heading = UiFactory.Create(WidgetKind.Text, page, -1, theme);
                heading.name = "Heading";
                var tmp = heading.GetComponent<TMP_Text>();
                Undo.RecordObject(tmp, "Add page");
                UiFactory.StyleText(tmp, TextStyle.Heading, theme);
                tmp.text = page.name;

                var back = UiFactory.Create(WidgetKind.Button, page, -1, theme);
                back.name = "Back";
                var label = back.GetComponentInChildren<TMP_Text>();
                Undo.RecordObject(label, "Add page");
                label.text = "Back";
                var a = SetChoice(back.GetComponent<Button>(), ActionChoice.OpenPage);
                Edit(a, "Add page", x => { x.page = pages[0].gameObject; RefreshPages(x); });
            }

            ShowPage(page);
            RefreshAllPages(panel);
            Undo.CollapseUndoOperations(group);
            return page;
        }

        private static Transform NewPage(string name, Transform root, UiTheme theme)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = root.gameObject.layer;
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = theme.spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            go.transform.SetParent(root, false);
            Undo.RegisterCreatedObjectUndo(go, "Add page");
            return go.transform;
        }

        // ------------------------------------------------------------------ checks

        public struct Problem
        {
            public string text;
            public Transform where;
            public System.Action fix;
        }

        /// <summary>Everything that would stop a panel's actions from working in VRChat.</summary>
        public static List<Problem> Check(Canvas panel)
        {
            var list = new List<Problem>();
            if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(NekoUiAction)) == null)
                list.Add(new Problem { text = "The NekoUiAction program asset is missing. Reinstall the package in the Creator Companion." });
            if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
                list.Add(new Problem { text = "An UdonSharp script in the project has compile errors, so no Udon runs. Check the Console." });

            foreach (var a in panel.GetComponentsInChildren<NekoUiAction>(true))
            {
                var s = a.GetComponent<Selectable>();
                var where = a.transform;
                if (s && !IsWired(s, a))
                    list.Add(new Problem { text = "\"" + a.name + "\" isn't connected to its action.", where = where, fix = () => Wire(s, a) });

                var choice = ChoiceOf(a);
                switch (choice)
                {
                    case ActionChoice.Show:
                    case ActionChoice.Hide:
                    case ActionChoice.ShowHide:
                    case ActionChoice.Mirror:
                        if (a.targets == null || a.targets.Length == 0 || a.targets.Any(t => !t))
                            list.Add(new Problem { text = "\"" + a.name + "\" has no object (or an empty slot) to " + (choice == ActionChoice.Mirror ? "switch" : "show/hide") + ".", where = where });
                        break;
                    case ActionChoice.PlaySound:
                    case ActionChoice.StopSound:
                        if (!a.sound) list.Add(new Problem { text = "\"" + a.name + "\" has no sound picked.", where = where });
                        break;
                    case ActionChoice.Teleport:
                        if (!a.teleportTarget) list.Add(new Problem { text = "\"" + a.name + "\" has no teleport point.", where = where });
                        break;
                    case ActionChoice.OpenPage:
                        if (!a.page) list.Add(new Problem { text = "\"" + a.name + "\" has no page picked.", where = where });
                        else if (a.pages == null || !a.pages.Contains(a.page) || a.pages.Length != UiWidgets.PagesOf(a.page.transform.parent).Count)
                            list.Add(new Problem { text = "\"" + a.name + "\" has an out-of-date page list.", where = where, fix = () => Edit(a, "Update pages", RefreshPages) });
                        break;
                    case ActionChoice.ShowOne:
                        if (a.targets == null || a.targets.Length == 0 || a.targets.Any(t => !t))
                            list.Add(new Problem { text = "\"" + a.name + "\" has an empty group (or an empty slot).", where = where });
                        break;
                    case ActionChoice.CustomEvent:
                        if (!a.eventTarget || string.IsNullOrEmpty(a.eventName))
                            list.Add(new Problem { text = "\"" + a.name + "\" needs an Udon script and an event name.", where = where });
                        break;
                }
            }
            return list;
        }
    }
}

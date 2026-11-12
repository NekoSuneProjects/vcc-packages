using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        // ------------------------------------------------------------------ action section (buttons and toggles)

        private void DrawActionFields(Selectable control)
        {
            bool isToggle = control is Toggle;
            EditorGUILayout.Space(6);
            G.Group(isToggle ? "When it's switched" : "When it's clicked", true);

            var a = UiActions.Get(control);
            var choice = UiActions.ChoiceOf(a);
            var picked = (ActionChoice)EditorGUILayout.Popup(new GUIContent("Do this"), (int)choice, UiActions.ChoiceLabels);
            G.Api("NekoUiAction.action · Button.onClick → UdonBehaviour.SendCustomEvent(\"Run\")");
            if (picked != choice)
            {
                a = UiActions.SetChoice(control, picked);
                choice = picked;
                GUIUtility.ExitGUI();
            }
            if (!a)
            {
                G.Hint(isToggle ? "Pick what switching this toggle does." : "Pick what clicking this button does.");
                return;
            }

            switch (choice)
            {
                case ActionChoice.Show:
                case ActionChoice.Hide:
                case ActionChoice.ShowHide:
                    DrawTargets(a, choice, isToggle);
                    break;
                case ActionChoice.Mirror: DrawMirror(a); break;
                case ActionChoice.PlaySound:
                case ActionChoice.StopSound: DrawSound(a, isToggle); break;
                case ActionChoice.Teleport: DrawTeleport(a); break;
                case ActionChoice.OpenPage: DrawPagePick(a); break;
                case ActionChoice.CustomEvent: DrawCustomEvent(a); break;
                case ActionChoice.ShowOne: DrawShowOne(a); break;
            }

            DrawToastOption(a);

            if (UiActions.CanSync(choice))
            {
                EditorGUI.BeginChangeCheck();
                bool synced = EditorGUILayout.ToggleLeft(new GUIContent(" Everyone sees it (synced)", "UdonSynced: the change is sent to every player, and people who join later get it too."), a.synced);
                G.Api("NekoUiAction.synced → [UdonSynced] + RequestSerialization");
                if (EditorGUI.EndChangeCheck()) UiActions.Edit(a, "Synced", x => x.synced = synced);
                G.Hint(a.synced
                    ? "Synced: when one player clicks, it changes for everyone in the instance, including people who join later."
                    : "Local: it only changes for the player who clicked. Good for personal settings.");
            }
            else
            {
                G.Hint(choice == ActionChoice.Teleport ? "Teleports only move the player who clicked." : "Pages are local: every player browses the menu on their own.");
            }

            if (!UiActions.IsWired(control, a))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Report("Not connected to the " + (isToggle ? "toggle" : "button") + " yet.", false);
                    if (GUILayout.Button("Connect", GUILayout.Width(70))) UiActions.Wire(control, a);
                }
            }
        }

        private void DrawTargets(NekoUiAction a, ActionChoice choice, bool isToggle)
        {
            var targets = (a.targets ?? new GameObject[0]).ToList();
            int remove = -1;
            for (int i = 0; i < targets.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var go = (GameObject)EditorGUILayout.ObjectField(i == 0 ? new GUIContent("Object", "Drag an object from the Hierarchy.") : new GUIContent(" "), targets[i], typeof(GameObject), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        int index = i;
                        UiActions.Edit(a, "Pick object", x => { var t = x.targets.ToArray(); t[index] = go; x.targets = t; });
                    }
                    if (GUILayout.Button(new GUIContent("✕", "Remove"), EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0) UiActions.Edit(a, "Remove object", x => x.targets = x.targets.Where((_, i) => i != remove).ToArray());
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                if (GUILayout.Button(targets.Count == 0 ? "+ Pick an object" : "+ Another object", EditorStyles.miniButton))
                    UiActions.Edit(a, "Add object", x => x.targets = (x.targets ?? new GameObject[0]).Concat(new GameObject[] { null }).ToArray());
            }
            if (isToggle)
                G.Hint(choice == ActionChoice.Hide ? "Toggle on = hidden, off = shown." : "Toggle on = shown, off = hidden.");
            else if (choice == ActionChoice.ShowHide)
                G.Hint("Each click switches between shown and hidden.");
        }

        private void DrawMirror(NekoUiAction a)
        {
            var mirrors = UiActions.FindMirrors();
            if (mirrors.Count == 0)
            {
                G.Report("No VRChat mirror in the scene yet. Add one (VRC Mirror Reflection), then pick it here.", false);
                return;
            }
            var current = a.targets != null && a.targets.Length > 0 ? a.targets[0] : null;
            int index = Mathf.Max(0, mirrors.IndexOf(current));
            int picked = EditorGUILayout.Popup(new GUIContent("Mirror"), index, mirrors.Select(m => m.name).ToArray());
            if (mirrors[picked] != current) UiActions.Edit(a, "Pick mirror", x => x.targets = new[] { mirrors[picked] });
            G.Hint("Mirrors are heavy for performance, so most worlds start them off and let each player turn them on (local).");
        }

        private void DrawSound(NekoUiAction a, bool isToggle)
        {
            EditorGUI.BeginChangeCheck();
            var src = (AudioSource)EditorGUILayout.ObjectField(new GUIContent("Sound", "An Audio Source in the scene."), a.sound, typeof(AudioSource), true);
            if (EditorGUI.EndChangeCheck()) UiActions.Edit(a, "Pick sound", x => x.sound = src);
            if (isToggle) G.Hint("Toggle on = playing, off = stopped.");
        }

        private void DrawTeleport(NekoUiAction a)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var t = (Transform)EditorGUILayout.ObjectField(new GUIContent("Teleport to", "An empty object; its blue arrow (forward) is where the player faces."), a.teleportTarget, typeof(Transform), true);
                if (EditorGUI.EndChangeCheck()) UiActions.Edit(a, "Pick teleport point", x => x.teleportTarget = t);
                if (GUILayout.Button(new GUIContent("New", "Make a teleport point at the centre of the Scene view."), EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    var go = new GameObject("Teleport Point");
                    var view = SceneView.lastActiveSceneView;
                    if (view) go.transform.position = view.pivot;
                    Undo.RegisterCreatedObjectUndo(go, "Make teleport point");
                    UiActions.Edit(a, "Pick teleport point", x => x.teleportTarget = go.transform);
                    EditorGUIUtility.PingObject(go);
                }
            }
            G.Hint("Put the point on the floor; the player lands standing on it.");
        }

        private void DrawPagePick(NekoUiAction a)
        {
            var pages = UiWidgets.PagesOf(panel.transform);
            if (pages.Count == 0)
            {
                G.Report("This panel has no pages yet. Click Page in the Add card to make one.", false);
                return;
            }
            int index = a.page ? pages.IndexOf(a.page.transform) : -1;
            int picked = EditorGUILayout.Popup(new GUIContent("Page"), index, pages.Select(p => p.name).ToArray());
            if (picked != index && picked >= 0)
                UiActions.Edit(a, "Pick page", x => { x.page = pages[picked].gameObject; UiActions.RefreshPages(x); });
        }

        private void DrawCustomEvent(NekoUiAction a)
        {
            EditorGUI.BeginChangeCheck();
            var target = (UdonBehaviour)EditorGUILayout.ObjectField(new GUIContent("Udon script", "The UdonBehaviour to send the event to."), a.eventTarget, typeof(UdonBehaviour), true);
            string evt = EditorGUILayout.TextField(new GUIContent("Event name", "A public method name in that script, e.g. ResetGame."), a.eventName);
            if (EditorGUI.EndChangeCheck()) UiActions.Edit(a, "Custom event", x => { x.eventTarget = target; x.eventName = evt; });
            G.Hint("For your own UdonSharp scripts: drag the object that has the script, and type the name of a public method.");
        }

        private void DrawShowOne(NekoUiAction a)
        {
            DrawTargets(a, ActionChoice.ShowOne, false);
            var group = (a.targets ?? new GameObject[0]).Where(t => t).ToList();
            var names = new[] { "(none – hide them all)" }.Concat(group.Select(g => g.name)).ToArray();
            int index = a.page ? group.IndexOf(a.page) + 1 : 0;
            int picked = EditorGUILayout.Popup(new GUIContent("Show", "The one object to show; the rest of the group is hidden."), index, names);
            G.Api("NekoUiAction.page (shown) · targets (the group)");
            if (picked != index) UiActions.Edit(a, "Pick shown object", x => x.page = picked == 0 ? null : group[picked - 1]);
            G.Hint("Example: a mirror group (HQ, LQ) with one button each for HQ, LQ and Off.");
        }

        private void DrawToastOption(NekoUiAction a)
        {
            var toasts = UiActions.FindToasts();
            if (toasts.Count == 0 && !a.toast) return;
            EditorGUI.BeginChangeCheck();
            bool on = EditorGUILayout.ToggleLeft(new GUIContent(" Then show a message", "Pops up a short message with the scene's Message popup (NekoToast)."), a.toast);
            if (EditorGUI.EndChangeCheck())
                UiActions.Edit(a, "Message", x => { x.toast = on ? toasts.FirstOrDefault() : null; if (on && string.IsNullOrEmpty(x.toastText)) x.toastText = "Done!"; });
            if (!a.toast || toasts.Count == 0) return;
            EditorGUI.BeginChangeCheck();
            int index = Mathf.Max(0, toasts.IndexOf(a.toast));
            int picked = toasts.Count > 1 ? EditorGUILayout.Popup("Popup", index, toasts.Select(t => t.name).ToArray()) : index;
            string text = EditorGUILayout.TextField("Message", a.toastText);
            G.Api("NekoUiAction.toast → NekoToast.Show(toastText)");
            if (EditorGUI.EndChangeCheck()) UiActions.Edit(a, "Message", x => { x.toast = toasts[picked]; x.toastText = text; });
        }

        // ------------------------------------------------------------------ slider actions

        private void DrawSliderActions(Slider slider)
        {
            EditorGUILayout.Space(6);
            G.Group("When it's moved", true);
            var a = UiActions.GetSlider(slider);
            var choice = UiActions.SliderChoiceOf(a);
            var picked = (SliderChoice)EditorGUILayout.Popup(new GUIContent("Do this"), (int)choice, UiActions.SliderLabels);
            G.Api("NekoUiSlider.action · Slider.onValueChanged → UdonBehaviour.SendCustomEvent(\"Changed\")");
            if (picked != choice)
            {
                UiActions.SetSliderChoice(slider, picked);
                GUIUtility.ExitGUI();
            }
            if (!a) { G.Hint("Pick what moving this slider does, e.g. music volume."); return; }

            switch (picked)
            {
                case SliderChoice.SoundVolume:
                {
                    var sounds = (a.sounds ?? new AudioSource[0]).ToList();
                    bool changed = false;
                    int remove = -1;
                    for (int i = 0; i < sounds.Count; i++)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var s = (AudioSource)EditorGUILayout.ObjectField(i == 0 ? new GUIContent("Sound") : new GUIContent(" "), sounds[i], typeof(AudioSource), true);
                            if (s != sounds[i]) { sounds[i] = s; changed = true; }
                            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) remove = i;
                        }
                    if (remove >= 0) { sounds.RemoveAt(remove); changed = true; }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                        if (GUILayout.Button(sounds.Count == 0 ? "+ Pick a sound" : "+ Another sound", EditorStyles.miniButton)) { sounds.Add(null); changed = true; }
                    }
                    if (changed) UiActions.EditSlider(a, "Sounds", x => x.sounds = sounds.ToArray());
                    G.Hint("Set the slider's range to 0 – 1 for volume.");
                    break;
                }
                case SliderChoice.AnimatorFloat:
                {
                    EditorGUI.BeginChangeCheck();
                    var anim = (Animator)EditorGUILayout.ObjectField("Animator", a.animator, typeof(Animator), true);
                    string param = EditorGUILayout.TextField(new GUIContent("Parameter", "A Float parameter in the Animator Controller."), a.parameter);
                    if (EditorGUI.EndChangeCheck()) UiActions.EditSlider(a, "Animator", x => { x.animator = anim; x.parameter = param; });
                    break;
                }
                case SliderChoice.SendToUdon:
                {
                    EditorGUI.BeginChangeCheck();
                    var target = (VRC.Udon.UdonBehaviour)EditorGUILayout.ObjectField("Udon script", a.eventTarget, typeof(VRC.Udon.UdonBehaviour), true);
                    string variable = EditorGUILayout.TextField(new GUIContent("Float variable", "A public float field in that script."), a.valueVariable);
                    string evt = EditorGUILayout.TextField(new GUIContent("Then send event", "Optional public method to run after the value is set."), a.eventName);
                    if (EditorGUI.EndChangeCheck()) UiActions.EditSlider(a, "Udon", x => { x.eventTarget = target; x.valueVariable = variable; x.eventName = evt; });
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            bool synced = EditorGUILayout.ToggleLeft(new GUIContent(" Everyone sees it (synced)"), a.synced);
            var valueText = (TMPro.TextMeshProUGUI)EditorGUILayout.ObjectField(new GUIContent("Show value in", "Optional text that shows the value, e.g. 75%."), a.valueText, typeof(TMPro.TextMeshProUGUI), true);
            if (EditorGUI.EndChangeCheck()) UiActions.EditSlider(a, "Slider", x => { x.synced = synced; x.valueText = valueText; });
            if (!UiActions.IsSliderWired(slider, a))
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Report("Not connected to the slider yet.", false);
                    if (GUILayout.Button("Connect", GUILayout.Width(70))) UiActions.WireSlider(slider, a);
                }
        }

        // ------------------------------------------------------------------ page card

        private void DrawPageFields(Transform page)
        {
            bool shown = page.gameObject.activeSelf;
            using (new EditorGUILayout.HorizontalScope())
            {
                G.Chip(shown ? "Showing now" : "Hidden", shown ? S.Success : S.Muted);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(shown))
                    if (GUILayout.Button(new GUIContent("Show this page", "Shows this page and hides the others, so you can work on it."), GUILayout.Width(120)))
                        UiActions.ShowPage(page);
            }
            var pages = UiWidgets.PagesOf(page.parent);
            if (pages.IndexOf(page) == 0)
                G.Hint("This is the first page. Before uploading, show it here so players see it first.");
            var v = page.GetComponent<VerticalLayoutGroup>();
            EditorGUI.BeginChangeCheck();
            float spacing = EditorGUILayout.Slider("Gap between items", v.spacing, 0, 80);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(v, "Edit page"); v.spacing = spacing; }

            int links = panel.GetComponentsInChildren<NekoUiAction>(true).Count(x => x.action == NekoUiAction.OpenPage && x.page == page.gameObject);
            if (links == 0 && pages.IndexOf(page) > 0)
                G.Report("No button opens this page yet. Pick a button, choose \"Open a page\" and this page.", false);
            G.Hint("While this is selected, new items from the Add card go inside it.");
        }

        // ------------------------------------------------------------------ panel check

        private void DrawActionCheck()
        {
            int count = panel.GetComponentsInChildren<NekoUiAction>(true).Length;
            if (count == 0) return;
            var problems = UiActions.Check(panel);
            if (problems.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Chip("✓ " + count + (count == 1 ? " action ready" : " actions ready"), S.Success, "Every button/toggle action is connected and has what it needs.");
                    GUILayout.FlexibleSpace();
                }
                return;
            }
            foreach (var p in problems)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    G.Report(p.text, false);
                    if (p.where && GUILayout.Button("Show", GUILayout.Width(48))) SelectFromOutline(p.where);
                    if (p.fix != null && GUILayout.Button("Fix", GUILayout.Width(40))) p.fix();
                }
            }
        }

        /// <summary>Selecting from the Assistant also reveals the page the item is on.</summary>
        private void SelectFromOutline(Transform t)
        {
            for (var x = t; x && x != panel.transform; x = x.parent)
                if (UiWidgets.Detect(x) == WidgetKind.Page && !x.gameObject.activeSelf) { UiActions.ShowPage(x); break; }
            Select(t);
        }
    }
}

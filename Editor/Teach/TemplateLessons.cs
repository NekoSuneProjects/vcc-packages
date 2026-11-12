using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using L = NekoSune.WorldUiAssistant.LessonBook;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Lessons for templates. Each one: create the template, look at how it works, change it, then
    /// rebuild a simpler version by hand (checked on a canvas the Assistant didn't make).
    /// </summary>
    internal static partial class SystemLessons
    {
        static partial void AddTemplateLessons(List<Lesson> list)
        {
            Boards(list);
            Systems(list);
        }

        // ------------------------------------------------------------------ shared helpers

        internal static Canvas Panel(string name) => L.WorldCanvases().FirstOrDefault(c => c.name.StartsWith(name));

        internal static IEnumerable<Canvas> HandMade() => L.WorldCanvases().Where(c => !AssistantWindow.IsAssistantPanel(c));

        internal static bool HandMadeHas(Func<Canvas, bool> test) => HandMade().Any(c => { try { return test(c); } catch { return false; } });

        internal static bool Calls(UnityEngine.Events.UnityEventBase evt, string method) =>
            Enumerable.Range(0, evt.GetPersistentEventCount()).Any(i => evt.GetPersistentMethodName(i) == method);

        internal static LessonStep CreateStep(string templateTitle, string panelName) => new LessonStep
        {
            text = "Open the <b>Templates</b> tab and create the <b>" + templateTitle + "</b>. It appears in front of the Scene view camera.",
            showMeLabel = "Create it for me",
            showMe = c =>
            {
                var t = TemplateLibrary.All.FirstOrDefault(x => x.title == templateTitle);
                if (t != null) TemplateLibrary.Create(t, UiThemeId.NekoDark);
            },
            check = c => Panel(panelName), checkHint = "Create the " + templateTitle + " from the Templates tab.",
        };

        internal static LessonStep SelectStep(string text, Func<Component> find, Func<bool> check, string hint) => new LessonStep
        {
            text = text,
            showMeLabel = "Show me",
            showMe = c => { var x = find(); if (x) { UnityEditor.Selection.activeGameObject = x.gameObject; L.OpenInspector(); } },
            check = c => check(), checkHint = hint,
        };

        internal static Lesson Make(string id, string templateId, string title, string summary, string badge, int minutes, params LessonStep[] steps) =>
            new Lesson { id = id, templateId = templateId, title = title, summary = summary, badge = badge, minutes = minutes, category = "Templates", steps = steps };

        // ------------------------------------------------------------------ Phase 4 boards and menus

        private static void Boards(List<Lesson> list)
        {
            list.Add(Make("t-welcome", "welcome", "Welcome board", "Pages, and buttons that switch them.", "Host", 8,
                CreateStep("Welcome board", "Welcome Board"),
                new LessonStep
                {
                    text = "The board has three <b>pages</b>: Home, Rules and Credits. In the outline, Rules and Credits are greyed out – they're inactive until a button opens them.",
                    quiz = new[] { "All three pages are visible at once", "Only one page is active; Open-a-page buttons switch them" },
                    answer = 1, quizWhy = "Inactive objects take no space in a layout, so the panel shows just the active page.",
                },
                new LessonStep
                {
                    text = "Change the welcome text to say something about <i>your</i> world.",
                    check = c => Panel("Welcome Board")?.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Text" && !t.text.StartsWith("Make yourself at home")) == true,
                    checkHint = "Select the welcome text in the outline and change it.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> make your own canvas (GameObject → UI → Canvas, World Space, scale 0.001) with a Button and an object to show. In the Button's <b>On Click</b>, add the object and pick <b>GameObject → SetActive</b> (tick the box). That's a page switch without Udon – VRChat allows SetActive from UI.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<Button>(true).Any(b => Calls(b.onClick, "SetActive"))),
                    checkHint = "A button on a hand-made canvas whose On Click calls GameObject.SetActive.",
                }));

            list.Add(Make("t-rules", "rules", "Rules board", "Stacking text with a Vertical Layout Group.", "Rule Keeper", 6,
                CreateStep("Rules board", "Rules Board"),
                new LessonStep
                {
                    text = "Each rule is its own text. The panel's Vertical Layout Group stacks them and adds the gap.",
                    quiz = new[] { "Move each rule by hand", "Add another text – the layout places it under the others" },
                    answer = 1, quizWhy = "Layout groups do the positioning, so adding or reordering is easy.",
                },
                new LessonStep
                {
                    text = "Add a sixth rule with the <b>Text</b> button in the Add card (select rule 5 first so it goes after it).",
                    check = c => Panel("Rules Board")?.GetComponentsInChildren<TMP_Text>(true).Count(t => t.text.Length > 2 && char.IsDigit(t.text[0])) >= 6
                                 || Panel("Rules Board")?.GetComponentsInChildren<TMP_Text>(true).Length >= 9,
                    checkHint = "Add one more text to the Rules board.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> on your own canvas add <b>Vertical Layout Group</b> (Add Component), then four <b>Text - TextMeshPro</b> objects inside. Untick Child Force Expand Height and tick Control Child Size.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<VerticalLayoutGroup>(true).Any(v => v.GetComponentsInChildren<TMP_Text>(true).Length >= 4)),
                    checkHint = "A Vertical Layout Group with at least four texts on a hand-made canvas.",
                }));

            list.Add(Make("t-settings", "settings", "Settings menu", "Toggles, a volume slider and mirror modes.", "Settings Pro", 10,
                CreateStep("Settings menu", "Settings Menu"),
                new LessonStep
                {
                    text = "The <b>Mirror</b> buttons use <b>Show one of a group</b>: HQ shows the HQ mirror and hides LQ; Off hides both. The mirrors were created for you (off) under <i>World UI Assistant – Objects</i>.",
                    quiz = new[] { "Mirrors are local here: each player picks their own", "Turning a mirror on turns it on for everyone" },
                    answer = 0, quizWhy = "Mirrors are expensive, so each player decides for themselves.",
                },
                new LessonStep
                {
                    text = "Pick your music: select the <b>Music Volume</b> slider and set its Sound (under When it's moved) to your world's Audio Source.",
                    check = c => Panel("Settings Menu")?.GetComponentsInChildren<NekoUiSlider>(true).Any(s => s.sounds != null && s.sounds.Any(x => x)) == true,
                    checkHint = "Pick an Audio Source for the Music Volume slider.",
                },
                new LessonStep
                {
                    text = "Fill in the <b>Pens</b>, <b>Chairs</b> and <b>Colliders</b> toggles with the objects they should hide (or delete the ones you don't need). The panel card's action check shows what's still missing.",
                    check = c => { var p = Panel("Settings Menu"); return p && UiActions.Check(p).Count(x => x.text.Contains("Pens") || x.text.Contains("Chairs") || x.text.Contains("Colliders")) == 0; },
                    checkHint = "Give Pens, Chairs and Colliders objects to hide, or delete those toggles.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> add a <b>Toggle</b> to your own canvas. In its <b>On Value Changed (Boolean)</b>, add an object and pick <b>GameObject → SetActive</b> from the <i>Dynamic bool</i> section – the toggle now shows/hides it.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<Toggle>(true).Any(t => Calls(t.onValueChanged, "SetActive"))),
                    checkHint = "A Toggle on a hand-made canvas whose On Value Changed calls GameObject.SetActive.",
                }));

            list.Add(Make("t-teleport", "teleport", "Teleport menu", "Teleport points and the TeleportTo call.", "Traveller", 7,
                CreateStep("Teleport menu", "Teleport Menu"),
                new LessonStep
                {
                    text = "Find the teleport points under <b>World UI Assistant – Objects</b> and move them to real places in your world. The blue arrow (forward) is where the player will face.",
                    check = c => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name.StartsWith("Teleport Point")) > 0,
                    checkHint = "Create the Teleport menu first.",
                },
                new LessonStep
                {
                    text = "In NekoUiAction.cs, teleporting is one line: <b>Networking.LocalPlayer.TeleportTo(position, rotation)</b>.",
                    quiz = new[] { "Everyone is teleported", "Only the player who clicked" },
                    answer = 1, quizWhy = "Networking.LocalPlayer is the player running this copy of the script – the one who clicked.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> on your own canvas add a Button, then <b>Add Component → Neko Ui Action</b>. Set <i>Action</i> to 5 (TeleportPlayer) and <i>Teleport Target</i> to a point. In On Click, drag the button in and pick <b>UdonBehaviour → SendCustomEvent</b> with the text <b>Run</b>.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<NekoUiAction>(true).Any(a => a.action == NekoUiAction.TeleportPlayer && a.teleportTarget
                                                  && a.GetComponent<Button>() && Calls(a.GetComponent<Button>().onClick, "SendCustomEvent"))),
                    checkHint = "A hand-made button with a NekoUiAction set to TeleportPlayer, wired with SendCustomEvent.",
                }));

            list.Add(Make("t-credits", "credits", "Credits board", "Rich text: bold, colours and sizes inside one text.", "Credit Where Due", 5,
                CreateStep("Credits board", "Credits Board"),
                new LessonStep
                {
                    text = "The credits use <b>rich text</b> tags like &lt;b&gt;bold&lt;/b&gt; inside one text. TextMeshPro also understands &lt;color=#ff8800&gt; and &lt;size=120%&gt;.",
                    quiz = new[] { "<b>World by</b> makes those words bold", "It shows the letters <b> on screen" },
                    answer = 0, quizWhy = "Rich Text is on for TextMeshPro texts, so tags change the style instead of being shown.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> on your own canvas add a Text - TextMeshPro and type a line with a <b>&lt;color=…&gt;</b> tag, e.g. <i>Made by &lt;color=#ff66aa&gt;you&lt;/color&gt;</i>.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("<color="))),
                    checkHint = "A TextMeshPro text with a <color=…> tag on a hand-made canvas.",
                }));

            list.Add(Make("t-schedule", "schedule", "Event schedule", "Rows: two texts side by side.", "Planner", 5,
                CreateStep("Event schedule", "Event Schedule"),
                new LessonStep
                {
                    text = "Each line is a <b>Row</b> (Horizontal Layout Group) with a time and an event. Change one event to something you're actually hosting.",
                    check = c => Panel("Event Schedule")?.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "Event" && !new[] { "Chill & chat", "Dance night", "Movie night", "World hopping", "Photo meetup" }.Contains(t.text)) == true,
                    checkHint = "Change the text of one of the events.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> on your own canvas add an empty object with a <b>Horizontal Layout Group</b> and two texts inside it.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<HorizontalLayoutGroup>(true).Any(h => !h.GetComponent<Toggle>() && h.GetComponentsInChildren<TMP_Text>(true).Length >= 2)),
                    checkHint = "A Horizontal Layout Group with two texts on a hand-made canvas.",
                }));

            list.Add(Make("t-links", "links", "Links / QR board", "Why VRChat links need QR codes.", "Link Master", 6,
                CreateStep("Links / QR board", "Links Board"),
                new LessonStep
                {
                    text = "Udon can't open a web browser, so links are shown as QR codes (scan with a phone) and in a read-only text field players can copy from on desktop.",
                    quiz = new[] { "Udon opens the link in the player's browser", "Players scan the QR code with their phone or copy the text" },
                    answer = 1, quizWhy = "For safety, VRChat worlds can't open web pages. (VRChat store products are different: Store.OpenListing opens them inside VRChat.)",
                },
                new LessonStep
                {
                    text = "Make a QR code for your own link with the <b>QR code maker</b> in the Templates tab and add it to a panel.",
                    check = c => L.WorldCanvases().Any(x => x.GetComponentsInChildren<Image>(true).Any(i => i.sprite && UnityEditor.AssetDatabase.GetAssetPath(i.sprite).Contains("/QR/"))),
                    checkHint = "Add a QR picture from the QR code maker to a panel.",
                },
                new LessonStep
                {
                    text = "<b>By hand:</b> on your own canvas add an <b>Image</b> showing one of the QR pictures (they're in Assets/NekoSune/UIAssistant/Generated/QR) and an <b>Input Field - TextMeshPro</b> with the link, set to Read Only.",
                    check = c => HandMadeHas(x => x.GetComponentsInChildren<Image>(true).Any(i => i.sprite && UnityEditor.AssetDatabase.GetAssetPath(i.sprite).Contains("/QR/"))
                                                && x.GetComponentsInChildren<TMP_InputField>(true).Any(f => f.readOnly)),
                    checkHint = "A QR Image and a read-only TMP Input Field on a hand-made canvas.",
                }));
        }
    }
}

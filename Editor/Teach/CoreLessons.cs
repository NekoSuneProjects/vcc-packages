using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using L = NekoSune.WorldUiAssistant.LessonBook;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>The eight core lessons from TODO.md Phase 3. They use the user's own panel.</summary>
    internal static class CoreLessons
    {
        private static bool Near(float a, float b, float tol = 0.5f) => Mathf.Abs(a - b) <= tol;
        private static bool Near(Vector2 a, Vector2 b, float tol = 0.02f) => Vector2.Distance(a, b) <= tol;

        private static void SelectPanel(LessonContext c)
        {
            if (!c.panel) return;
            Selection.activeGameObject = c.panel.gameObject;
            L.OpenInspector();
        }

        public static List<Lesson> Build() => new List<Lesson>
        {
            // 1 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "canvas", category = "Basics", minutes = 5, badge = "Canvas Cadet",
                title = "1. What a Canvas is",
                summary = "Why VRChat worlds use World Space canvases, and how big they are.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Every Unity UI lives on a <b>Canvas</b>. Each panel the Assistant makes is one Canvas.\n\nMake a panel in the Build tab, or pick one under <b>Your panels</b>.",
                        highlight = "Your panels",
                        check = c => c.panel, checkHint = "Pick or create a panel in the Build tab first.",
                    },
                    new LessonStep
                    {
                        text = "Select the panel and look at it in Unity's <b>Inspector</b>. Find the <b>Canvas</b> component: its <b>Render Mode</b> is <b>World Space</b>.",
                        showMeLabel = "Select it and open the Inspector", showMe = SelectPanel,
                        check = c => c.panel && Selection.activeGameObject == c.panel.gameObject,
                        checkHint = "Select the panel itself (click its name under Your panels → Select).",
                    },
                    new LessonStep
                    {
                        text = "Unity has three Render Modes. Screen Space draws on top of the camera view; World Space makes the UI an object in the 3D world.",
                        quiz = new[] { "World Space is faster to type", "So the UI is an object in the world that players can walk up to and point at", "Screen Space can't have buttons" },
                        answer = 1, quizWhy = "In VRChat nobody can see your screen overlay – a world UI has to exist in the world, like a sign.",
                    },
                    new LessonStep
                    {
                        text = "The panel's <b>Scale</b> is 0.001. That makes <b>1 UI unit = 1 mm</b>. Its <b>Width/Height</b> are in those units.\n\nIn the Panel card, set the width to <b>1000</b> (= 1 metre). Watch it grow in the Scene view.",
                        highlight = "Panel",
                        check = c => c.panel && Near(((RectTransform)c.panel.transform).sizeDelta.x, 1000f, 1f),
                        checkHint = "Set Size (mm) to 1000 × something in the Panel card.",
                    },
                    new LessonStep
                    {
                        text = "Text sizes use the same units.",
                        quiz = new[] { "30 cm tall", "About 3 cm (30 mm) tall", "30 m tall" },
                        answer = 1, quizWhy = "30 UI units × 0.001 scale = 0.03 m. Readable from about 2 m away.",
                    },
                    new LessonStep
                    {
                        text = "Back in the Inspector, find <b>Graphic Raycaster</b> and <b>VRC Ui Shape</b> on the panel.",
                        showMeLabel = "Show the panel in the Inspector", showMe = SelectPanel,
                        quiz = new[] { "Canvas Scaler", "VRC Ui Shape", "Image" },
                        answer = 1, quizWhy = "VRCUiShape makes the VRChat laser able to hit the canvas; Graphic Raycaster sends those hits to the buttons.",
                    },
                },
            },

            // 2 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "recttransform", category = "Basics", minutes = 8, badge = "Anchor Ace",
                title = "2. RectTransform: anchors, pivot, size",
                summary = "Pin a badge to a corner by hand, outside the layout.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Every UI object has a <b>Rect Transform</b>: anchors (where it's pinned in its parent), pivot (its own centre point) and size.\n\nSelect any item in your panel and look at its Rect Transform. The values are grey: <i>\"Some values driven by VerticalLayoutGroup\"</i> – the layout sets them for you.",
                        highlight = "Outline", showMeLabel = "Select a text and open the Inspector",
                        showMe = c => { L.SelectFirst<TMP_Text>(c); L.OpenInspector(); },
                        check = c => c.panel && Selection.activeTransform && Selection.activeTransform.IsChildOf(c.panel.transform) && Selection.activeTransform != c.panel.transform,
                        checkHint = "Select something inside your panel.",
                    },
                    new LessonStep
                    {
                        text = "Now place something yourself. Add an <b>Image</b> from the Add card. In the Inspector click <b>Add Component → Layout Element</b> and tick <b>Ignore Layout</b>. The layout group stops moving it.",
                        highlight = "Add",
                        check = c => L.InPanel<LayoutElement>(c).Any(e => e.ignoreLayout && e.GetComponent<Image>()),
                        checkHint = "Add a Layout Element with Ignore Layout ticked to an Image in your panel.",
                    },
                    new LessonStep
                    {
                        text = "Click the <b>anchor preset</b> square at the top-left of its Rect Transform and pick the <b>top-right</b> one. Both anchors are now (1, 1): pinned to the top-right corner.",
                        check = c => L.InPanel<LayoutElement>(c).Any(e => e.ignoreLayout && Near(((RectTransform)e.transform).anchorMin, Vector2.one) && Near(((RectTransform)e.transform).anchorMax, Vector2.one)),
                        checkHint = "Set Anchors Min and Max of that image to X 1, Y 1.",
                    },
                    new LessonStep
                    {
                        text = "Set its <b>Pivot</b> to X 1, Y 1 and its <b>Pos X / Pos Y</b> to -20 / -20, with Width/Height 120. It now sits just inside the corner – a perfect \"New!\" badge.",
                        check = c => L.InPanel<LayoutElement>(c).Any(e =>
                        {
                            var r = (RectTransform)e.transform;
                            return e.ignoreLayout && Near(r.pivot, Vector2.one) && r.anchoredPosition.x < 0 && r.anchoredPosition.y < 0;
                        }),
                        checkHint = "Pivot 1, 1 and a negative Pos X and Pos Y on the corner image.",
                    },
                    new LessonStep
                    {
                        text = "Make the panel wider in the Panel card and watch the badge.",
                        quiz = new[] { "It stays the same distance from the top-right corner", "It stays in the centre", "It stretches" },
                        answer = 0, quizWhy = "Both anchors are at the corner, so it keeps its distance from that corner. Anchors spread apart (min ≠ max) make it stretch instead.",
                    },
                },
            },

            // 3 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "layout", category = "Basics", minutes = 8, badge = "Layout Wrangler",
                title = "3. Layout groups and fitters",
                summary = "Rows, gaps and things that grow with their content.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Your panel has a <b>Vertical Layout Group</b>: it stacks everything top to bottom.\n\nSet <b>Gap between items</b> to <b>30</b> in the Panel card.",
                        highlight = "Panel",
                        check = c => c.panel && c.panel.GetComponent<VerticalLayoutGroup>() && Near(c.panel.GetComponent<VerticalLayoutGroup>().spacing, 30f),
                        checkHint = "Set Gap between items to 30 in the Panel card.",
                    },
                    new LessonStep
                    {
                        text = "Add a <b>Row</b> (a Horizontal Layout Group). It starts with two buttons. Select the row and add one more <b>Button</b> – it goes inside.",
                        highlight = "Add",
                        check = c => L.InPanel<HorizontalLayoutGroup>(c).Any(h => !h.GetComponent<Toggle>() && h.transform.childCount >= 3),
                        checkHint = "A Row with at least three items inside.",
                    },
                    new LessonStep
                    {
                        text = "Select the row and turn off <b>Share the width equally</b> (Unity: Child Force Expand Width). The buttons shrink to the size they <i>want</i>.",
                        highlight = "Edit",
                        check = c => L.InPanel<HorizontalLayoutGroup>(c).Any(h => !h.GetComponent<Toggle>() && !h.childForceExpandWidth),
                        checkHint = "Untick Share the width equally on the row.",
                    },
                    new LessonStep
                    {
                        text = "How big a child <i>wants</i> to be comes from its content (text length) or from a component you can add to set it.",
                        quiz = new[] { "Layout Element", "Mask", "Canvas" },
                        answer = 0, quizWhy = "Layout Element sets Min / Preferred / Flexible width and height for a layout group.",
                    },
                    new LessonStep
                    {
                        text = "Add a <b>Scroll list</b>. In the Hierarchy open <b>Scroll List → Viewport → Content</b>: it has a <b>Content Size Fitter</b>, so Content grows as you add items and the list can scroll.",
                        highlight = "Add",
                        showMeLabel = "Select the Content object",
                        showMe = c =>
                        {
                            var sr = L.InPanel<ScrollRect>(c).FirstOrDefault();
                            if (sr && sr.content) { Selection.activeGameObject = sr.content.gameObject; L.OpenInspector(); }
                        },
                        check = c => L.InPanel<ScrollRect>(c).Any(s => s.content && s.content.GetComponent<ContentSizeFitter>()),
                        checkHint = "Add a Scroll list from the Add card.",
                    },
                },
            },

            // 4 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "events", category = "Basics", minutes = 6, badge = "Event Wizard",
                title = "4. Buttons, events and SendCustomEvent",
                summary = "What happens when you click, and why VRChat needs SendCustomEvent.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Select a button and give it an action under <b>When it's clicked</b>, e.g. <b>Show / hide something</b> with your title as the object.",
                        highlight = "Edit",
                        check = c => L.InPanel<NekoUiAction>(c).Any(a => a.GetComponent<Button>()),
                        checkHint = "Give a button an action in its Edit card.",
                    },
                    new LessonStep
                    {
                        text = "In the Inspector, find the Button's <b>On Click ()</b> list. It calls <b>UdonBehaviour → SendCustomEvent</b> with the text <b>Run</b>.",
                        showMeLabel = "Select the button and open the Inspector",
                        showMe = c =>
                        {
                            var a = L.InPanel<NekoUiAction>(c).FirstOrDefault(x => x.GetComponent<Button>());
                            if (a) { Selection.activeGameObject = a.gameObject; L.OpenInspector(); }
                        },
                        check = c => L.Selected<NekoUiAction>(), checkHint = "Select the button that has the action.",
                    },
                    new LessonStep
                    {
                        text = "Why doesn't On Click call the script's Run() method directly?",
                        quiz = new[] { "VRChat UI may only call Udon through UdonBehaviour.SendCustomEvent", "Run is private", "Buttons can't call methods" },
                        answer = 0, quizWhy = "For safety VRChat only lets UI events call a short list of methods. SendCustomEvent(\"Run\") then runs Run() inside Udon.",
                    },
                    new LessonStep
                    {
                        text = "Try it: press <b>Play</b>, click the button in the Game view (ClientSim), then stop Play mode.\n\nWhen you're done, press Next.",
                    },
                },
            },

            // 5 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "udon", category = "Basics", minutes = 10, badge = "Udon Reader",
                title = "5. Udon basics: reading the action script",
                summary = "Read NekoUiAction.cs – the script behind every action.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Open the script. Read the comment at the top and find the method <b>Run()</b>.",
                        showMeLabel = "Open NekoUiAction.cs",
                        showMe = c =>
                        {
                            var script = AssetDatabase.FindAssets("NekoUiAction t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("NekoUiAction.cs"));
                            if (script != null) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(script));
                        },
                        quiz = new[] { "Start()", "Run()", "OnDeserialization()" },
                        answer = 1, quizWhy = "On Click sends the event \"Run\", so Run() is what runs.",
                    },
                    new LessonStep
                    {
                        text = "Look at the line <b>public GameObject[] targets;</b>",
                        quiz = new[] { "A list of objects you fill in the Inspector", "It creates new objects", "It deletes objects" },
                        answer = 0, quizWhy = "Public fields show up in the Inspector. The Assistant fills them in for you.",
                    },
                    new LessonStep
                    {
                        text = "Find the part of Run() for <b>TeleportPlayer</b>.",
                        quiz = new[] { "Everyone is teleported", "Only the player who clicked (Networking.LocalPlayer.TeleportTo)", "Nothing happens" },
                        answer = 1, quizWhy = "Networking.LocalPlayer is always the player running this copy of the script – the one who clicked.",
                    },
                    new LessonStep
                    {
                        text = "Select a button with an action. In the Inspector, the <b>Neko Ui Action (U# Script)</b> component shows exactly the fields from the script, filled in by the Assistant.",
                        showMeLabel = "Select it",
                        showMe = c =>
                        {
                            var a = L.InPanel<NekoUiAction>(c).FirstOrDefault();
                            if (a) { Selection.activeGameObject = a.gameObject; L.OpenInspector(); }
                        },
                        check = c => L.Selected<NekoUiAction>(), checkHint = "Select a button or toggle that has an action.",
                    },
                },
            },

            // 6 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "sync", category = "Basics", minutes = 6, badge = "Sync Savvy",
                title = "6. Syncing: local vs networked UI",
                summary = "Who sees what, and what late joiners get.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "A player opens a page of your menu.",
                        quiz = new[] { "Everyone sees the new page", "Only that player (it's local)" },
                        answer = 1, quizWhy = "Pages are local so every player can browse on their own.",
                    },
                    new LessonStep
                    {
                        text = "Pick a button with a show/hide action and tick <b>Everyone sees it (synced)</b>.",
                        highlight = "Edit",
                        check = c => L.InPanel<NekoUiAction>(c).Any(a => a.synced),
                        checkHint = "Tick Everyone sees it (synced) on an action.",
                    },
                    new LessonStep
                    {
                        text = "Someone joins after the object was hidden with a synced action.",
                        quiz = new[] { "They see it hidden – synced variables are sent to late joiners", "They see it shown – they missed it" },
                        answer = 0, quizWhy = "The state is kept in an [UdonSynced] variable, and VRChat sends synced variables to new players.",
                    },
                    new LessonStep
                    {
                        text = "Open NekoUiAction.cs and find <b>ShareState</b>.",
                        quiz = new[] { "RequestSerialization", "SendCustomEvent", "Destroy" },
                        answer = 0, quizWhy = "With manual sync, RequestSerialization sends the synced variables to everyone.",
                    },
                    new LessonStep
                    {
                        text = "ShareState calls Networking.SetOwner first.",
                        quiz = new[] { "Only the owner can change synced variables", "The instance master always changes them", "The first player in the world" },
                        answer = 0, quizWhy = "Every synced object has one owner; whoever clicks takes ownership, then changes the value.",
                    },
                },
            },

            // 7 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "performance", category = "Basics", minutes = 7, badge = "Frame Saver",
                title = "7. Performance and Quest limits",
                summary = "Canvas count, raycast targets, texture sizes and overdraw.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Each Canvas is extra work every frame. Combine small signs into one panel where you can. A handful is fine; dozens is not.",
                        check = c => L.WorldCanvases().Count() <= 12,
                        checkHint = "Your scene has many world canvases. Merge some panels (move items into one panel and delete the empty ones).",
                    },
                    new LessonStep
                    {
                        text = "Text and decorations shouldn't be <b>Raycast Targets</b> (they'd catch laser hits for nothing). Check your panel: every text outside buttons should have Raycast Target off. The <b>Doctor</b> tab can fix it for you.",
                        check = c => !L.InPanel<TMP_Text>(c).Any(t => t.raycastTarget && !t.GetComponentInParent<Selectable>(true)),
                        checkHint = "A text still has Raycast Target on. Untick it in the Inspector (Extra Settings), or use the Doctor tab.",
                    },
                    new LessonStep
                    {
                        text = "Big textures cost memory, and Quest has little. Keep UI pictures at 1024 px or less; 2048 is the most you should use.",
                        check = c => !L.InPanel<Image>(c).Any(i => i.sprite && i.sprite.texture && Mathf.Max(i.sprite.texture.width, i.sprite.texture.height) > 2048),
                        checkHint = "An image in your panel uses a texture bigger than 2048 px. Lower its Max Size in the texture's import settings.",
                    },
                    new LessonStep
                    {
                        text = "Transparent layers drawn on top of each other (overdraw) are slow on Quest.",
                        quiz = new[] { "One panel with a few solid images", "Ten stacked half-transparent glass layers" },
                        answer = 0, quizWhy = "Every transparent layer redraws the same pixels again.",
                    },
                    new LessonStep
                    {
                        text = "The Assistant sets every button's <b>Navigation</b> to None.",
                        quiz = new[] { "It stops stuck highlights and odd focus jumps in VR", "It makes the button load faster" },
                        answer = 0, quizWhy = "Unity's keyboard navigation keeps buttons 'selected' after a laser click, which looks broken in VR.",
                    },
                },
            },

            // 8 ----------------------------------------------------------------------------------
            new Lesson
            {
                id = "graduation", category = "Basics", minutes = 15, badge = "UI Graduate",
                title = "8. Graduation: build a panel by hand",
                summary = "Everything you learned, without the Assistant.",
                steps = new[]
                {
                    new LessonStep
                    {
                        text = "Without the Assistant: <b>GameObject → UI → Canvas</b>. Set its Render Mode to <b>World Space</b>.",
                        check = c => HandMade().Any(),
                        checkHint = "Create a Canvas with GameObject → UI → Canvas and set Render Mode to World Space.",
                    },
                    new LessonStep
                    {
                        text = "Set its Scale to <b>0.001</b> on X, Y and Z, Width 800, Height 600, and move it in front of the camera.",
                        check = c => HandMade().Any(x => Near(x.transform.localScale.x, 0.001f, 0.0002f) && ((RectTransform)x.transform).sizeDelta.x >= 200),
                        checkHint = "Scale 0.001 and a sensible width (e.g. 800).",
                    },
                    new LessonStep
                    {
                        text = "Add <b>VRC Ui Shape</b> (Add Component), keep its <b>Graphic Raycaster</b>, and put it on the <b>UI</b> layer.",
                        check = c => HandMade().Any(x => UiFactory.UiShapeType != null && x.GetComponent(UiFactory.UiShapeType) && x.GetComponent<GraphicRaycaster>() && x.gameObject.layer == UiFactory.UiLayer),
                        checkHint = "VRC Ui Shape + Graphic Raycaster on the canvas, layer UI.",
                    },
                    new LessonStep
                    {
                        text = "Right-click the canvas → <b>UI → Button - TextMeshPro</b>. Set the button's <b>Navigation</b> to None.",
                        check = c => HandMade().Any(x => x.GetComponentsInChildren<Button>(true).Any(b => b.navigation.mode == Navigation.Mode.None)),
                        checkHint = "A button inside your hand-made canvas with Navigation = None.",
                    },
                    new LessonStep
                    {
                        text = "Give it something to call: add an UdonSharp script (or a NekoUiAction) to an object, then in the button's <b>On Click</b> add an entry: drag that object in, pick <b>UdonBehaviour → SendCustomEvent (string)</b> and type the method name.",
                        check = c => HandMade().Any(x => x.GetComponentsInChildren<Button>(true).Any(b =>
                            Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentTarget(i) is UdonBehaviour && b.onClick.GetPersistentMethodName(i) == "SendCustomEvent"))),
                        checkHint = "The button's On Click should call UdonBehaviour.SendCustomEvent.",
                    },
                    new LessonStep
                    {
                        text = "<b>You built a VRChat world UI by hand.</b> You don't need the Assistant any more – but it's still here to save time.\n\nTry the <b>Expert</b> help level: no hints, just tools.",
                        showMeLabel = "Switch to Expert", showMe = c => Help.Level = HintLevel.Expert,
                    },
                },
            },
        };

        /// <summary>World canvases the Assistant didn't create.</summary>
        private static IEnumerable<Canvas> HandMade() => L.WorldCanvases().Where(c => !AssistantWindow.IsAssistantPanel(c));
    }
}

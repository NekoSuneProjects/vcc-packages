namespace NekoSune.WorldUiAssistant
{
    /// <summary>Every term the Assistant uses, in plain English.</summary>
    internal static class Glossary
    {
        public static readonly (string term, string meaning)[] Terms =
        {
            ("Anchor", "Where a UI object is pinned inside its parent (e.g. top-right corner). When the parent resizes, the object keeps its distance from its anchor. Layout groups set anchors for you."),
            ("Canvas", "The root of every Unity UI. Everything inside it is drawn as UI. A world panel is one Canvas."),
            ("Canvas Scaler", "Controls how a Canvas scales its content. For World Space canvases it mostly sets text sharpness."),
            ("Child / Parent", "Objects in the Hierarchy can be inside other objects. The inside one is the child; it moves and hides with its parent."),
            ("Content Size Fitter", "Makes an object as big as its content, e.g. a list that grows when you add items."),
            ("Creator Economy", "VRChat's built-in store: players buy products from you inside VRChat. Udon can check who owns a product (UdonProduct)."),
            ("Draw call", "One batch of drawing work for the graphics card. Fewer draw calls = better frame rate, especially on Quest."),
            ("EventSystem", "Unity object that sends clicks to UI. VRChat brings its own in-game; the one in your scene is for testing in the editor."),
            ("GameObject", "Any object in your scene. Components on it decide what it is and does."),
            ("Component", "A piece of behaviour on a GameObject: Image, Button, Canvas, an Udon script…"),
            ("Graphic Raycaster", "Lets pointers (your mouse, the VRChat laser) hit the UI on a Canvas. Without it, nothing can be clicked."),
            ("Grid Layout Group", "Places children in equal-sized cells, row by row."),
            ("Horizontal Layout Group", "Places children side by side. The Assistant calls it a Row."),
            ("Image", "Shows a picture (Sprite) or a solid colour. Buttons, backgrounds and dividers are Images."),
            ("Instance", "One copy of your world that players are in together. Synced things are shared within an instance."),
            ("Instance master", "The player who has been in the instance longest. Often used for admin-only buttons."),
            ("Late joiner", "A player who joins after something happened. Synced variables are sent to them; one-off events are not."),
            ("Layer", "A group objects belong to, used for rendering and collisions. VRChat world UI should be on the UI layer."),
            ("Layout Element", "Tells a layout group how big this child wants to be (minimum, preferred, flexible size)."),
            ("Local", "Only happens for the player who did it. Opening your own menu is local."),
            ("Mask", "Hides anything outside its shape. Scroll lists use one so items don't spill out."),
            ("Navigation", "Unity's keyboard/gamepad focus between buttons. In VR it causes stuck highlights, so VRChat UI sets it to None."),
            ("Overdraw", "Drawing the same pixel many times (stacked transparent images). Costs performance, especially on Quest."),
            ("Owner", "The player who controls an object's synced data. Only the owner can change synced variables; Networking.SetOwner changes it."),
            ("Persistence", "Saving data for a player between visits (PlayerData / PlayerObjects), e.g. \"don't show again\" or high scores."),
            ("Pivot", "The point an object rotates and scales around, and where its position is measured from."),
            ("Prefab", "A saved, reusable GameObject (with its children). Drag it into scenes as often as you like."),
            ("Quest", "Meta Quest / Android VRChat. Weaker hardware: keep textures small, few transparent layers and few canvases."),
            ("Raycast Target", "Whether a Graphic can be hit by pointers. Turn it off on text and decorations so clicks reach the buttons."),
            ("Rect Transform", "The position and size of a UI object: anchors, pivot, width/height. UI's version of Transform."),
            ("RequestSerialization", "Udon call that sends this object's synced variables to everyone (manual sync)."),
            ("Scroll Rect", "Makes content scrollable inside a viewport. The Assistant calls it a Scroll list."),
            ("SendCustomEvent", "Runs a public method by name on an UdonBehaviour. VRChat UI buttons must call Udon this way."),
            ("SendCustomNetworkEvent", "Like SendCustomEvent, but runs the method on other players' copies too. Not received by late joiners."),
            ("Sprite", "A texture imported for UI (Texture Type: Sprite (2D and UI))."),
            ("Synced", "Shared with everyone in the instance. In Udon: [UdonSynced] variables plus RequestSerialization."),
            ("TextMeshPro", "Unity's sharp, flexible text system (TMP). The Assistant uses TextMeshPro - Text (UI)."),
            ("Udon", "VRChat's scripting system for worlds. Scripts run safely inside VRChat."),
            ("UdonBehaviour", "The component that runs an Udon program on an object."),
            ("UdonSharp", "Write Udon in C#. The Assistant's scripts (NekoUiAction and friends) are UdonSharp."),
            ("UI layer", "The Unity layer for UI. VRChat's laser and UI rendering expect world UI on it."),
            ("VRCUiShape", "VRChat component that makes the VRChat laser able to point at a Canvas."),
            ("VRCUrl", "A web address Udon is allowed to use. URLs typed in at runtime must come from a VRCUrlInputField."),
            ("Vertical Layout Group", "Stacks children top to bottom. Panels and pages use one."),
            ("World Space", "Canvas mode where the UI is an object in the 3D world (not stuck to the screen). VRChat world UI uses it."),
        };
    }

    /// <summary>Plain-English notes for components the Assistant creates, used by "What did it make?".</summary>
    internal static class ComponentGuide
    {
        public static string Explain(string typeName)
        {
            switch (typeName)
            {
                case "RectTransform": return "Position and size of every UI object.";
                case "Canvas": return "Root of the UI. World Space puts it in the 3D world.";
                case "CanvasScaler": return "Scaling and text sharpness of the canvas.";
                case "GraphicRaycaster": return "Lets the laser and mouse hit this canvas.";
                case "VRCUiShape": return "Lets the VRChat laser point at this canvas.";
                case "CanvasRenderer": return "Draws a UI Graphic. Added automatically with Image/Text.";
                case "Image": return "Picture or colour: backgrounds, buttons, dividers.";
                case "TextMeshProUGUI": return "Text (TextMeshPro).";
                case "Button": return "Clickable; its On Click event runs actions.";
                case "Toggle": return "On/off switch; On Value Changed runs actions.";
                case "Slider": return "Drag to pick a value.";
                case "ScrollRect": return "Scrolls its Content inside the Viewport.";
                case "Scrollbar": return "The scroll list's bar.";
                case "Mask": return "Hides what's outside the viewport.";
                case "VerticalLayoutGroup": return "Stacks children top to bottom.";
                case "HorizontalLayoutGroup": return "Places children side by side.";
                case "GridLayoutGroup": return "Places children in a grid.";
                case "LayoutElement": return "Size wishes for a layout group (min/preferred height).";
                case "ContentSizeFitter": return "Grows to fit its content.";
                case "UdonBehaviour": return "Runs an Udon program (the action scripts).";
                case "NekoUiAction": return "The Assistant's action script: what a button/toggle does.";
                default: return typeName.StartsWith("Neko") ? "An Assistant Udon script for a ready-made system." : "";
            }
        }
    }
}

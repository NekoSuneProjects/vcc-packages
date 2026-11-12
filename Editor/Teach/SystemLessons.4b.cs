using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using L = NekoSune.WorldUiAssistant.LessonBook;

namespace NekoSune.WorldUiAssistant
{
    internal static partial class SystemLessons
    {
        /// <summary>The string a UI event entry passes (e.g. SendCustomEvent's event name).</summary>
        internal static string StringArg(Object owner, string eventProperty, int index)
        {
            var so = new SerializedObject(owner);
            var p = so.FindProperty(eventProperty + ".m_PersistentCalls.m_Calls.Array.data[" + index + "].m_Arguments.m_StringArgument");
            return p != null ? p.stringValue : null;
        }

        /// <summary>True if a hand-made canvas has a button that sends an event whose name starts with <paramref name="prefix"/>.</summary>
        internal static bool HandButtonSends(string prefix, int atLeast = 1) => HandMadeHas(c =>
            c.GetComponentsInChildren<Button>(true).Count(b =>
                Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i =>
                    b.onClick.GetPersistentMethodName(i) == "SendCustomEvent" && (StringArg(b, "m_OnClick", i) ?? "").StartsWith(prefix))) >= atLeast);

        internal static bool InHandMade(Component c) => c && HandMade().Any(h => c.transform.IsChildOf(h.transform));

        private static LessonStep Quiz(string text, string[] options, int answer, string why) =>
            new LessonStep { text = text, quiz = options, answer = answer, quizWhy = why };

        private static LessonStep ByHand(string text, System.Func<bool> check, string hint) =>
            new LessonStep { text = "<b>By hand:</b> " + text, check = c => check(), checkHint = hint };

        private const string WireText = "In its On Click, drag in the object with the script, pick <b>UdonBehaviour → SendCustomEvent (string)</b> and type ";

        private static void Systems(List<Lesson> list)
        {
            // ------------------------------------------------------------------ Media

            list.Add(Make("s-video", "video", "Video player UI", "How VRChat video players sync, and why URLs must be VRCUrl.", "Projectionist", 12,
                CreateStep("Video player controls", "Video Controls"),
                Quiz("Players paste links into the <b>URL field</b>, a VRCUrlInputField – not a normal text field.",
                    new[] { "Any text field works for links", "Udon may only play URLs that come from a VRCUrlInputField or were typed in the editor as VRCUrl" }, 1,
                    "For safety, VRChat only lets worlds load URLs a player typed into a VRCUrlInputField, or VRCUrls you set in the editor (like the playlist)."),
                Quiz("When the owner presses Play, NekoVideoPanel shares the URL and the <b>server time</b> the video started.",
                    new[] { "Everyone else jumps to (server time now − start time)", "Everyone starts from 0:00" }, 0,
                    "Using Networking.GetServerTimeInSeconds() means late joiners land at the right moment, and drift is fixed every few seconds."),
                new LessonStep
                {
                    text = "Add a video to the playlist: open the <b>System settings</b> card, add an entry to <i>Playlist</i> and <i>Titles</i>, then press Next ▶ in Play mode.",
                    check = c => Panel("Video Controls")?.GetComponent<NekoVideoPanel>()?.playlist?.Any(u => u != null && !string.IsNullOrEmpty(u.Get())) == true,
                    checkHint = "Add a URL to the Playlist in the System settings card.",
                },
                ByHand("make a canvas with a <b>VRCUrlInputField</b> (Add Component → VRC Url Input Field on an Input Field object). That's the only way players can give Udon a new link.",
                    () => HandMadeHas(c => c.GetComponentsInChildren<VRCUrlInputField>(true).Any()), "A VRCUrlInputField on a hand-made canvas.")));

            list.Add(Make("s-music", "music", "Music player", "AudioSources, playlists and shuffle.", "DJ", 7,
                CreateStep("Music player", "Music Player"),
                new LessonStep
                {
                    text = "Add your songs: in the <b>System settings</b> card, drag audio files into <i>Tracks</i>.",
                    check = c => Panel("Music Player")?.GetComponent<NekoMusicPlayer>()?.tracks?.Any(t => t) == true,
                    checkHint = "Add at least one AudioClip to Tracks.",
                },
                Quiz("Shuffle picks a random track that isn't the current one. With <b>synced</b> on…",
                    new[] { "everyone hears the same track (the owner decides)", "each player hears a different random track" }, 0,
                    "The owner shares the track number and start time; everyone else follows."),
                ByHand("on your own canvas, add a Button whose On Click calls <b>AudioSource → Play</b> on an object with an Audio Source. (VRChat allows a few safe Unity calls like this straight from UI.)",
                    () => HandMadeHas(c => c.GetComponentsInChildren<Button>(true).Any(b => Calls(b.onClick, "Play"))), "A hand-made button calling AudioSource.Play.")));

            list.Add(Make("s-slideshow", "slideshow", "Gallery / slideshow", "Sprites, captions and synced presentations.", "Curator", 6,
                CreateStep("Gallery / slideshow", "Slideshow"),
                new LessonStep
                {
                    text = "Add pictures: import images, set <b>Texture Type</b> to <i>Sprite (2D and UI)</i>, and drag them into <i>Slides</i> in the System settings card.",
                    check = c => Panel("Slideshow")?.GetComponent<NekoSlideshow>()?.slides?.Any(s => s) == true,
                    checkHint = "Add at least one Sprite to Slides.",
                },
                Quiz("For a talk where you click through slides for everyone:",
                    new[] { "Tick Synced – the owner's slide is shared", "Leave it local" }, 0, "Synced shares the slide number; only the owner auto-advances."),
                ByHand("on your own canvas, put two Images and a Button whose On Click calls <b>GameObject → SetActive</b> on one of them – a tiny two-picture switcher.",
                    () => HandMadeHas(c => c.GetComponentsInChildren<Image>(true).Length >= 3 && c.GetComponentsInChildren<Button>(true).Any(b => Calls(b.onClick, "SetActive"))),
                    "Two images and a SetActive button on a hand-made canvas.")));

            // ------------------------------------------------------------------ Menus

            list.Add(Make("s-tabs", "tabs", "Tabbed menu", "Menus, tabs and sub-pages.", "Navigator", 6,
                CreateStep("Tabbed menu", "Tabbed Menu"),
                Quiz("The tab buttons use <b>Open a page</b>. Why is the tab row not hidden when you switch?",
                    new[] { "It's a Row, not a page – only pages (columns directly under the panel) are switched", "It's hidden but you can't see it" }, 0,
                    "Open-a-page only switches the panel's pages."),
                new LessonStep
                {
                    text = "Add a fourth tab: click <b>Page</b> in the Add card, then add a tab button in the Tabs row and set it to <b>Open a page</b> → your new page.",
                    check = c => Panel("Tabbed Menu") is Canvas p && UiWidgets.PagesOf(p.transform).Count >= 4,
                    checkHint = "Give the Tabbed menu a fourth page.",
                },
                ByHand("on your own canvas, make two buttons that each call <b>GameObject → SetActive</b> – one turns a page on, one turns it off.",
                    () => HandMadeHas(c => c.GetComponentsInChildren<Button>(true).Count(b => Calls(b.onClick, "SetActive")) >= 2), "Two SetActive buttons on a hand-made canvas.")));

            list.Add(Make("s-portable", "portable", "Portable menu", "Tracking data, and why portable UI is local-only.", "On the Go", 8,
                CreateStep("Portable menu", "Portable Menu"),
                Quiz("NekoPortableMenu places the panel using <b>GetTrackingData(Head)</b>.",
                    new[] { "It returns the position/rotation of the player's head (or hands)", "It returns the camera of the world" }, 0,
                    "Tracking data is where the local player's head and hands are, in VR and on desktop."),
                Quiz("Why is a portable menu local-only?",
                    new[] { "It follows *your* head – syncing it would drag everyone's menu to one player", "Udon can't sync menus" }, 0,
                    "Each player opens and moves their own copy."),
                new LessonStep
                {
                    text = "In the System settings card, set <b>Follow Mode</b> to 1 (follow the head) or 2/3 (on a hand), then try it in Play mode with M.",
                    check = c => Resources.FindObjectsOfTypeAll<NekoPortableMenu>().Any(p => p && p.gameObject.scene.IsValid() && p.followMode != NekoPortableMenu.StayWhereOpened),
                    checkHint = "Change Follow Mode on the portable menu controller.",
                },
                ByHand("add <b>Neko Portable Menu</b> (Add Component) to an empty object and set its <i>Menu</i> to a canvas you made yourself.",
                    () => Resources.FindObjectsOfTypeAll<NekoPortableMenu>().Any(p => p && p.menu && InHandMade(p.menu)), "A NekoPortableMenu whose Menu is a hand-made canvas.")));

            list.Add(Make("s-hotkeys", "hotkeys", "Hotkeys", "Udon input events vs Input.GetKeyDown.", "Shortcut Pro", 7,
                CreateStep("Hotkeys", "Hotkeys"),
                Quiz("<b>Input.GetKeyDown</b> reads the keyboard. Who can use keyboard hotkeys?",
                    new[] { "Desktop players", "VR players" }, 0, "VR players have no keyboard; VRChat gives them input events like InputJump and InputUse instead."),
                Quiz("For VR, NekoHotkeys can run one hotkey on a <b>double jump</b> (InputJump).",
                    new[] { "VR players can't rebind controller buttons, so worlds reuse VRChat's input events", "VR controllers have letter keys" }, 0,
                    "Input events are the only controller input Udon gets; pick gestures that don't break normal movement."),
                new LessonStep
                {
                    text = "Open the System settings card. Any key VRChat already uses shows a warning – make sure none do.",
                    check = c => Resources.FindObjectsOfTypeAll<NekoHotkeys>().Any(h => h && h.gameObject.scene.IsValid() && h.keys != null && h.keys.All(k => !new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.C, KeyCode.Z, KeyCode.V, KeyCode.R, KeyCode.Tab, KeyCode.Escape }.Contains((KeyCode)k))),
                    checkHint = "Remove keys that clash with VRChat.",
                },
                ByHand("add <b>Neko Hotkeys</b> to an object of your own, add one key (e.g. 75 = K) and a target UdonBehaviour.",
                    () => Resources.FindObjectsOfTypeAll<NekoHotkeys>().Any(h => h && h.gameObject.scene.IsValid() && !(h.GetComponentInParent<Canvas>(true) is Canvas cv && AssistantWindow.IsAssistantPanel(cv.rootCanvas)) && h.keys != null && h.keys.Length > 0 && h.targets != null && h.targets.Any(t => t)),
                    "A NekoHotkeys you added yourself, with a key and a target.")));

            list.Add(Make("s-admin", "admin", "Admin / moderator panel", "Owner-only controls and instance-master checks.", "Moderator", 6,
                CreateStep("Admin panel", "Admin Panel"),
                Quiz("Who is the <b>instance master</b>?",
                    new[] { "The player who has been in the instance the longest", "The world's creator, always" }, 0, "When they leave, the next-longest player becomes master – NekoAdminGate re-checks then."),
                Quiz("Can a hacker read your admin name list?",
                    new[] { "Yes – Udon data isn't secret, so admin buttons must be harmless", "No, it's encrypted" }, 0, "Never put secrets in a world; keep admin powers to things like resetting games."),
                ByHand("on your own canvas, add <b>Neko Admin Gate</b> and put a group of buttons in its <i>Admin Only</i> list.",
                    () => Resources.FindObjectsOfTypeAll<NekoAdminGate>().Any(g => g && InHandMade(g) && g.adminOnly != null && g.adminOnly.Any(a => a)), "A NekoAdminGate on a hand-made canvas with Admin Only set.")));

            // ------------------------------------------------------------------ Shop

            list.Add(Make("s-store", "store", "Store / market", "How VRChat purchases work, QR links and an updatable shop.", "Shopkeeper", 15,
                CreateStep("Store / market", "Store"),
                Quiz("For a <b>VRChat Creator Economy</b> product, the Buy button calls <b>Store.OpenListing</b>.",
                    new[] { "VRChat opens the real product page; the purchase happens in VRChat", "The world takes the payment itself" }, 0,
                    "Worlds never handle money. After buying, OnPurchaseConfirmed tells the world, and OnPurchasesLoaded tells it what each player owns when they join."),
                Quiz("For a Booth or Gumroad item, the detail page shows a <b>QR code</b> and copyable text.",
                    new[] { "Because Udon can't open a web browser", "Because QR codes are faster" }, 0, "Players scan the code with their phone, or copy the link on desktop."),
                new LessonStep
                {
                    text = "Make the shop yours: in the System settings card, change a product's <i>Names</i>, <i>Prices</i> and <i>External Urls</i>. For a Creator Economy product, drag its <b>UdonProduct</b> asset into <i>Products</i>.",
                    check = c => Panel("Store")?.GetComponent<NekoStore>()?.names?.Any(n => n != "Fox avatar" && n != "Neon world kit" && n != "Supporter pass" && n != "Cat ears" && n != "Shader pack" && n != "Sticker set") == true,
                    checkHint = "Rename at least one product in the System settings card.",
                },
                Quiz("To change prices without re-uploading the world, set <b>Data Url</b> to a JSON file you host.",
                    new[] { "The store downloads it with VRCStringDownloader when the world loads", "Udon can edit the world file" }, 0,
                    "Remote data updates text; pictures need predeclared VRCUrls (VRCImageDownloader) because worlds can't build URLs at runtime."),
                ByHand("on your own canvas, add a button that sends <b>OpenStorePage</b> to a NekoStore (" + WireText + "OpenStorePage).",
                    () => HandButtonSends("OpenStorePage") || HandButtonSends("Buy"), "A hand-made button sending OpenStorePage or Buy.")));

            // ------------------------------------------------------------------ Games & social

            list.Add(Make("s-keypad", "keypad", "Keypad / code door", "Forms and input: keypads and code doors.", "Codebreaker", 6,
                CreateStep("Keypad / code door", "Keypad"),
                new LessonStep
                {
                    text = "Change the <b>Code</b> in the System settings card, then try it in Play mode – the door cube disappears.",
                    check = c => Panel("Keypad")?.GetComponent<NekoKeypad>()?.code != "1234",
                    checkHint = "Change the code from 1234.",
                },
                Quiz("Is a keypad code safe for protecting secret areas?",
                    new[] { "No – anyone who rips the world can read the code", "Yes, it's hidden" }, 0, "Use it for fun puzzles, not real security."),
                ByHand("on your own canvas, make three buttons that send <b>Press1</b>, <b>Press2</b>, <b>Press3</b> to a NekoKeypad (" + WireText + "Press1 …).",
                    () => HandButtonSends("Press", 3), "Three hand-made buttons sending Press… events.")));

            list.Add(Make("s-scoreboard", "scoreboard", "Scoreboard / leaderboard", "Synced arrays and saving a personal best.", "Top Scorer", 8,
                CreateStep("Scoreboard / leaderboard", "Scoreboard"),
                Quiz("The board stores names and scores in <b>[UdonSynced] arrays</b>.",
                    new[] { "Whoever changes a score becomes the owner, edits the arrays and calls RequestSerialization", "Each player keeps their own board" }, 0,
                    "One shared board, kept in sync for everyone – including late joiners."),
                Quiz("Your <b>personal best</b> uses PlayerData.",
                    new[] { "It's saved for you between visits", "It's lost when you leave" }, 0, "PlayerData is VRChat persistence: per-player data that's still there next time (loaded before OnPlayerRestored)."),
                ByHand("on your own canvas, add a button that sends <b>AddPoint</b> to a NekoScoreboard (" + WireText + "AddPoint).",
                    () => HandButtonSends("AddPoint"), "A hand-made button sending AddPoint.")));

            list.Add(Make("s-hud", "hud", "Game HUD", "Score, lives and timers for mini-games.", "Game Maker", 7,
                CreateStep("Game HUD", "Game HUD"),
                Quiz("Your game script tells the HUD what happened by sending events.",
                    new[] { "e.g. AddScore when a target is hit, LoseLife when you fall", "The HUD watches the game by itself" }, 0,
                    "The HUD only displays; your game logic sends it events (or use the Test buttons)."),
                new LessonStep
                {
                    text = "Set <b>Round Seconds</b> to 30 in the System settings card, then play a round in Play mode.",
                    check = c => Mathf.Approximately(Panel("Game HUD")?.GetComponent<NekoGameHud>()?.roundSeconds ?? 0, 30f),
                    checkHint = "Set Round Seconds to 30.",
                },
                ByHand("on your own canvas, add a Start button that sends <b>StartGame</b> to a NekoGameHud.",
                    () => HandButtonSends("StartGame"), "A hand-made button sending StartGame.")));

            list.Add(Make("s-achievements", "achievements", "Achievements / progress", "Persistence: inventory, achievements, progress.", "Achiever", 7,
                CreateStep("Achievements / progress", "Achievements"),
                Quiz("Why does NekoAchievements wait for <b>OnPlayerRestored</b> before saving?",
                    new[] { "Your saved data isn't loaded until then – saving earlier could overwrite it", "It's just slower" }, 0,
                    "Always read PlayerData after OnPlayerRestored for the local player."),
                Quiz("How does a game unlock achievement 3?",
                    new[] { "Send the event Unlock2 (counting from 0) to the achievements script", "Call it by its title" }, 0, "Unlock0 … Unlock9 are public events any button or script can send."),
                ByHand("on your own canvas, add a button that sends <b>Unlock0</b> to a NekoAchievements.",
                    () => HandButtonSends("Unlock"), "A hand-made button sending Unlock0.")));

            list.Add(Make("s-poll", "poll", "Poll / voting", "Synced votes, one per player.", "Voice of the People", 7,
                CreateStep("Poll / voting", "Poll"),
                Quiz("How does the poll stop someone voting twice?",
                    new[] { "It stores each voter's playerId; voting again moves their vote", "It trusts players" }, 0, "playerId is unique per player in the instance."),
                new LessonStep
                {
                    text = "Change the question and options to your own (Title text and the vote buttons' labels, plus <i>Options</i> in the System settings card).",
                    check = c => Panel("Poll")?.GetComponent<NekoPoll>()?.options?.Any(o => o != "Movie night" && o != "Dance party" && o != "Game night") == true,
                    checkHint = "Change at least one option in the System settings card.",
                },
                ByHand("on your own canvas, add two buttons that send <b>Vote0</b> and <b>Vote1</b> to a NekoPoll.",
                    () => HandButtonSends("Vote", 2), "Two hand-made buttons sending Vote… events.")));

            list.Add(Make("s-dialogue", "dialogue", "Dialogue / NPC box", "Story text, typed out letter by letter.", "Storyteller", 6,
                CreateStep("Dialogue box", "Dialogue"),
                new LessonStep
                {
                    text = "Write your own lines in <i>Lines</i> (System settings card) and set the speaker names.",
                    check = c => Panel("Dialogue")?.GetComponent<NekoDialogue>()?.lines?.Any(l => l != null && !l.StartsWith("Hi there") && !l.StartsWith("The mirrors") && !l.StartsWith("Have fun")) == true,
                    checkHint = "Change at least one line.",
                },
                Quiz("For a guided tour where the guide clicks for everyone:",
                    new[] { "Tick Synced", "Leave it local" }, 0, "Synced shares the current line number."),
                ByHand("on your own canvas, add a button that sends <b>Next</b> to a NekoDialogue.",
                    () => HandButtonSends("Next") || HandButtonSends("Skip"), "A hand-made button sending Next.")));

            list.Add(Make("s-players", "players", "Player list & instance info", "OnPlayerJoined / OnPlayerLeft and player info.", "Host with the Most", 6,
                CreateStep("Player list & instance info", "Players"),
                Quiz("When do the join times start counting?",
                    new[] { "From when *you* arrived – players already here show \"before you\"", "From when the instance was created" }, 0,
                    "OnPlayerJoined fires for everyone already in the instance when you arrive, so earlier join times aren't known."),
                ByHand("on your own canvas, add a TextMeshPro text and drag it into a NekoPlayerList's <i>List Text</i>.",
                    () => Resources.FindObjectsOfTypeAll<NekoPlayerList>().Any(p => p && p.listText && InHandMade(p.listText)), "A NekoPlayerList whose List Text is on a hand-made canvas.")));

            list.Add(Make("s-avatars", "avatars", "Avatar board", "Avatar pedestals and avatar search boards.", "Fashionista", 7,
                CreateStep("Avatar board", "Avatars"),
                new LessonStep
                {
                    text = "Find the avatar pedestals under <b>World UI Assistant – Objects</b> and paste a public avatar's ID (avtr_…) into each one's <i>Blueprint Id</i>.",
                    check = c => Resources.FindObjectsOfTypeAll<VRCAvatarPedestal>().Any(p => p && p.gameObject.scene.IsValid() && !string.IsNullOrEmpty(p.blueprintId)),
                    checkHint = "Set a Blueprint Id on one of the pedestals.",
                },
                Quiz("The buttons call <b>SetAvatarUse</b> on a hidden pedestal.",
                    new[] { "It switches the clicking player into that avatar (if it's public or theirs)", "It copies the avatar into the world" }, 0,
                    "Pedestals are VRChat's safe way for worlds to offer avatars."),
                ByHand("on your own canvas, add a button that sends <b>Use0</b> to a NekoAvatarBoard.",
                    () => HandButtonSends("Use"), "A hand-made button sending Use0.")));

            // ------------------------------------------------------------------ Pieces

            list.Add(Make("s-map", "map", "Map – you are here", "Turning a world position into a spot on a map.", "Cartographer", 7,
                CreateStep("Map – you are here", "Map"),
                new LessonStep
                {
                    text = "Put a top-down picture of your world on <b>Map Picture</b>, then set <i>World Min</i> / <i>World Max</i> (X and Z of the corners) in the System settings card.",
                    check = c => Panel("Map")?.GetComponent<NekoMap>() is NekoMap m && (m.worldMin != new Vector2(-50, -50) || m.worldMax != new Vector2(50, 50)),
                    checkHint = "Change World Min or World Max to match your world.",
                },
                Quiz("The marker uses <b>Mathf.InverseLerp</b> on the player's X and Z.",
                    new[] { "It turns the position into 0–1 across the map, then into map pixels", "It teleports the player" }, 0, "0 = the min corner, 1 = the max corner."),
                ByHand("put a NekoMap's <i>Marker</i> on an Image inside a canvas you made yourself.",
                    () => Resources.FindObjectsOfTypeAll<NekoMap>().Any(m => m && m.marker && InHandMade(m.marker)), "A NekoMap whose Marker is on a hand-made canvas.")));

            list.Add(Make("s-photo", "photo", "Photo spot helper", "Delayed events: SendCustomEventDelayedSeconds.", "Photographer", 5,
                CreateStep("Photo spot helper", "Photo Spot"),
                Quiz("The countdown runs Tick() again a second later with <b>SendCustomEventDelayedSeconds</b>.",
                    new[] { "Udon has no coroutines, so delayed events are how you wait", "It uses Thread.Sleep" }, 0, "Delayed events are the Udon way to do something later."),
                ByHand("on your own canvas, add a button that sends <b>StartCountdown</b> to a NekoPhotoTimer.",
                    () => HandButtonSends("StartCountdown"), "A hand-made button sending StartCountdown.")));

            list.Add(Make("s-clock", "clock", "Clock & countdown", "Local time vs UTC for events.", "Timekeeper", 5,
                CreateStep("Clock & countdown", "Clock"),
                Quiz("Why is the event time in <b>UTC</b>?",
                    new[] { "So everyone counts down to the same moment, wherever they live", "Because Udon only knows UTC" }, 0, "The clock itself can still show each player's own local time."),
                new LessonStep
                {
                    text = "Set your next event's date and time (UTC) and name in the System settings card.",
                    check = c => Panel("Clock")?.GetComponent<NekoClock>()?.eventName != "Next event",
                    checkHint = "Give the event a name.",
                },
                ByHand("on your own canvas, add a TextMeshPro text and drag it into a NekoClock's <i>Clock Text</i>.",
                    () => Resources.FindObjectsOfTypeAll<NekoClock>().Any(k => k && k.clockText && InHandMade(k.clockText)), "A NekoClock whose Clock Text is on a hand-made canvas.")));

            list.Add(Make("s-toast", "toast", "Message popup", "Notifications and toasts.", "Herald", 5,
                CreateStep("Message popup", "Message Popup"),
                new LessonStep
                {
                    text = "Pick a button with an action and tick <b>Then show a message</b> under its action – e.g. \"Mirror on\".",
                    check = c => L.WorldCanvases().Any(x => x.GetComponentsInChildren<NekoUiAction>(true).Any(a => a.toast && !string.IsNullOrEmpty(a.toastText))),
                    checkHint = "Turn on \"Then show a message\" for an action.",
                },
                Quiz("If two messages come quickly, NekoToast…",
                    new[] { "shows the newest and hides it on the newest one's timer", "shows both on top of each other" }, 0, "Each Show schedules a hide; only the newest message's hide closes the box."),
                ByHand("on your own canvas, add a button with a <b>Neko Ui Action</b> whose <i>Toast</i> and <i>Toast Text</i> are set.",
                    () => Resources.FindObjectsOfTypeAll<NekoUiAction>().Any(a => a && InHandMade(a) && a.toast && !string.IsNullOrEmpty(a.toastText)), "A hand-made NekoUiAction with a toast message.")));

            list.Add(Make("s-welcome-once", "welcome-once", "Welcome with \"Don't show again\"", "Persistence for a board that remembers.", "Good Host", 5,
                CreateStep("Welcome with \"Don't show again\"", "Welcome (Don't Show Again)"),
                Quiz("When the rules change, how do you show the board to everyone again?",
                    new[] { "Change Save Key (e.g. …v2) – old \"hidden\" choices no longer match", "Ask everyone to clear their data" }, 0, "A new key starts fresh for everyone."),
                ByHand("on your own canvas, add a button that sends <b>DontShowAgain</b> to a NekoWelcomeOnce.",
                    () => HandButtonSends("DontShowAgain"), "A hand-made button sending DontShowAgain.")));
        }
    }
}

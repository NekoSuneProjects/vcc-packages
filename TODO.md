# NekoSune World UI Assistant – TODO

A from-scratch Unity editor tool that helps beginners **build** VRChat world UI visually and **teaches them how it works** over time, so they eventually don't need the tool.

Package: `com.nekosune.world-ui-assistant` · Branch: `world-ui-assistant` · Menu: `NekoSune → World → UI Assistant`

Status: **All phases built – 0.5.0 preview.** Open items need a person: testing in the VRChat client (desktop + VR), recording lesson videos, translating lesson/glossary text.

---

## Principles

- [x] **Beginner first.** No Unity UI knowledge assumed. Every screen says what it does and why.
- [x] **Teach, don't hide.** Everything the tool generates is normal Unity UI the user can open and edit. The tool explains what it made.
- [x] **Learn by doing.** Short lessons that use the user's own UI, not separate demo scenes.
- [x] **Safe.** Every change is one Undo step. Never edit the user's assets without asking.
- [x] **Standalone, like the other packages.** Own UI, NekoSuneVR footer, update checker, VCC package with release workflow.

## Lessons from the old World UI Builder (`dssdf/.../world-ui-builder`)

Reuse:
- [x] Theme presets (Neko Dark, Light, Neon, Glass, Pastel, Terminal). Custom: edit colours per item for now.
- [x] JSON blueprint export/import.
- [x] UI Doctor checks (World Space canvas, GraphicRaycaster, VRCUiShape, Navigation = None, small targets, small text).

Avoid:
- [x] Wiring buttons with a UnityEvent straight to an UdonSharp method. Use `UdonBehaviour.SendCustomEvent("Name")` instead (the likely bug in World Gallery / Avatar Search Auto-Wire).
  - The Assistant wires On Click → `UdonBehaviour.SendCustomEvent("Run")` on the backing UdonBehaviour, and this is tested in Play mode with ClientSim.
- [x] Encoding actions in GameObject names (`NUI[id|action|value]`). Store them in a small marker component or ScriptableObject.
  - Phase 1 needs no markers: widgets are recognised from their normal Unity components (Button, Toggle, layout groups…), so hand-made UI works too. VRChat only strips `IEditorOnly` components from **avatars**, so a world marker component would ship in the world. Phase 2 keeps action data in the `NekoUiAction` UdonSharp script itself – a real Udon component that ships with the world and needs the data at runtime anyway.
- [x] A static-only window that loses the user's work on script reload. The scene is the saved state; the window only keeps references.

---

## Phase 1 – MVP: build a panel without knowing Unity UI

- [x] Package skeleton: `package.json`, asmdef, README, CHANGELOG, `.meta` files, release workflow with the 3-way version check, `version.json`.
- [x] Update checker + NekoSuneVR footer.
- [x] **New panel wizard:** pick a size (small sign, menu, big board), a theme and a place in the scene (in front of the Scene view camera).
- [x] Generate a correct VRChat world canvas: World Space, sensible scale (1 px ≈ 1 mm), `GraphicRaycaster`, `VRCUiShape`, UI layer, Navigation = None.
- [x] **Widget palette:** Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Scroll list. Add with one click.
- [x] **Outline list:** reorder (drag, or drop into a Row/Grid/Scroll list), rename, show/hide, duplicate, delete. Clicking selects the real object, and selecting in the Hierarchy selects it in the outline.
- [x] **Inspector card per widget:** text, size, colours, font size, with plain-English labels and tooltips.
- [x] Layout handled for them: VerticalLayoutGroup on the panel (fixed panel size), ContentSizeFitter inside Scroll lists; Rows and Grids as simple choices.
- [x] Optional "grow with content" panel: a toggle in the Panel card adds a ContentSizeFitter (fixed size stays the default).
- [x] Live preview: changes show immediately in the Scene view; a **Frame panel** button.
- [x] Panel health line (World Space, GraphicRaycaster, VRCUiShape, UI layer) with one-click fixes – an early piece of the Phase 4 UI Doctor.
- [x] Environment card: warns when Unity isn't 2022.3, the Worlds SDK is missing, or TMP Essentials aren't imported (with an import button).
- [x] "How it works in Unity" line on every Edit card – an early piece of the Phase 3 teaching mode.

## Phase 2 – Make it do things (no code)

- [x] **Action picker** on buttons and toggles: Show / Hide / Show-or-hide objects, Turn a mirror on/off, Play / Stop a sound, Teleport the player, Open a page, Send an Udon event.
- [x] ~~Generate one UdonSharp helper per panel into `Assets/NekoSune/UIAssistant/Generated/`~~ → instead, one small, commented UdonSharp script ships with the package (`Runtime/NekoUiAction.cs` + program asset + U# assembly definition). Each button/toggle gets its own copy with its settings; On Click / On Value Changed call `SendCustomEvent("Run")`. No code generation, nothing written into Assets, and the script is the one Lesson 5 reads.
- [x] Local vs synced choice per action, with a one-line explanation. Synced uses `[UdonSynced]` + `RequestSerialization` (late joiners get the state); synced Udon events use `SendCustomNetworkEvent`. Teleports and pages are always local.
- [x] Toggles drive their action from their on/off state, and synced toggles update their checkbox for other players.
- [x] **Pages:** Page item adds a page with a heading and a working Back button; the first page wraps the existing content as Home. "Show this page" while editing; Open-a-page actions keep their page lists up to date.
- [x] Check after building: unconnected buttons (fix), missing objects/sounds/teleport points/pages, out-of-date page lists (fix), missing program asset, UdonSharp compile errors.
- [x] Tested in Play mode with ClientSim: show/hide, open page + Back, synced toggle.
- [x] Mirror quality (HQ/LQ/Off) and "create a mirror for me" – the Settings template uses the scene's mirrors or creates HQ and LQ mirrors from the SDK prefab.
- [x] Udon event picker listing the public methods of the chosen UdonSharp script (typed by name for Udon Graph scripts).
- [x] Animator parameter actions: set/switch a Bool, fire a Trigger, set an Int (parameters picked from the controller); local or synced.

## Phase 3 – Teaching mode (long-term learning)

- [x] **Explain mode toggle:** each setting shows *what Unity component/property it changes* in a line under it (e.g. `LayoutElement.preferredHeight`).
- [x] **"What did it make?" panel:** lists every generated component with a short explanation and a Select button.
- [x] **Lessons** (5–10 min each, use the user's own panel, step-by-step with highlights and a "check my work" button):
  1. What a Canvas is and why worlds use World Space
  2. RectTransform: anchors, pivot, size
  3. Layout groups and content size fitters
  4. Buttons, events and why VRChat needs `SendCustomEvent`
  5. Udon basics: reading the generated helper script
  6. Syncing: local vs networked UI
  7. Performance: overdraw, canvas count, texture sizes, Quest limits
  8. Building a UI by hand without the Assistant (graduation)
- [x] **Progress tracking** per machine (EditorPrefs): lessons done, badges, "next suggested lesson".
- [x] **Hint level setting:** Full help → Some help → Expert (hides explanations as the user improves).
- [x] Glossary page with every term used in the tool.

## Phase 4 – Doctor and polish

- [x] UI Doctor with one-click fixes (from the old builder, extended): missing raycaster, wrong canvas mode, missing VRCUiShape, Navigation on, tiny buttons, unreadable text, overlapping panels, Quest-heavy textures.
- [x] Templates: Welcome board, Rules, Settings (mirror/audio/post-processing), Teleport menu, Credits, Event schedule, Links/QR – each with a lesson that ends in a hand-built version.
- [x] QR code encoder (byte mode, level M, versions 1–10), verified by decoding with OpenCV.
- [x] JSON blueprint export/import and share.
- [x] Undo coverage for every action; no lost work on script reload.

## Phase 4b – Ready-made UI systems

Each one is a template the user drops in and customises with the same editor, and each has its own lesson.

### Video player UI
- [x] Control panel for a video player: Play/Pause, Stop, seek bar, time, volume, URL field, Sync button, Loop toggle.
- [x] Works with the stock VRChat players (AVPro / Unity Video) through the generated helper.
- [x] Detects popular community players (ProTV, USharpVideo, VideoTXL, YamaPlayer, VizVid…) by type name and points to their existing controls instead of replacing their sync logic.
- [x] Playlist (predeclared `VRCUrl[]`) with titles, thumbnails and Next/Previous.
- [x] Owner/master-only lock option, explained in plain English.
- [x] Lesson: how VRChat video players sync, and why URLs must be `VRCUrl`.

### Portable UI (follows the player)
- [x] Menu that opens near the player instead of being fixed in the world: in front of the face, on the wrist, or from the hand.
- [x] Open/close by button, gesture (VR) or hotkey (desktop); remembers the last page.
- [x] Follow modes: stays where opened, smoothly follows the head, or attaches to a hand.
- [x] Distance, scale and auto-hide timer settings; local-only by default (each player sees only their own).
- [ ] VR and desktop tested separately in the VRChat client (laser reach, readable size, doesn't block the view) – needs a real Build & Test.
- [x] Lesson: tracking data (`GetTrackingData`) and why portable UI is local-only.

### Hotkey toggles
- [x] Bind desktop keys (e.g. `M` mirror, `P` portable menu, `F1` help) to any UI action.
- [x] VR equivalents via VRChat input events (`InputUse`, `InputJump`, `InputLookVertical`…), with a note on what VRChat doesn't allow.
- [x] On-screen hint list of the world's hotkeys, toggleable by the player.
- [x] Conflict check against VRChat's own desktop keys.
- [x] Lesson: Udon input events vs `Input.GetKeyDown`.

### Store / market UI
- [x] Catalog builder: grid or list of product cards (image, name, price/tier, short description, badge such as "New"/"Owned").
- [x] Product detail page: large image gallery, **video preview** (reuses the video player UI), long description, buy/info button.
- [x] **Links attached to products.** Be honest about VRChat limits and pick the right option for each link:
  - [x] VRChat Creator Economy items: open the real store with `Store.OpenListing` / `Store.OpenWorldStorePage`, unlock content with `UdonProduct` ownership (reuse the World Economy lifecycle notes: wait for `OnPurchasesLoaded`, etc.).
  - [x] External shops (Booth, Gumroad, Jinxxy, Patreon…): Udon can't open a browser, so show the URL as text plus a generated **QR code** texture, and a copy-able text field.
- [x] Categories, filters and search (text field), sort by price/new.
- [x] Data from a remote JSON feed (VRCStringDownloader + VRCJson) updates names, prices, descriptions and categories without re-uploading. Product pictures from predeclared `VRCUrl`s (VRCImageDownloader, queued for the rate limit).
- [x] "Owned" state and unlocking world content for buyers (Creator Economy only).
- [x] Supporter / thank-you wall linked to the store.
- [x] Lessons: how VRChat purchases work, why external links need QR codes, and keeping a shop updatable with remote data.

### Everything else VRChat UI can do (coverage list)
Each gets a template **and** a lesson, so the Assistant covers the whole range of VRChat world UI.

- [x] Menus, tabs and sub-pages (core).
- [x] Forms and input: text fields, keypads, password/code doors.
- [x] Scoreboards and leaderboards (synced and persistent).
- [x] Polls and voting (synced, one vote per player).
- [x] Dialogue / NPC conversation boxes.
- [x] Maps and "you are here" boards with teleport points.
- [x] Inventory, achievements and progress screens (persistence).
- [x] Image galleries and slideshows (local Sprites; remote images → NekoSune World Gallery package).
- [x] Avatar pedestals and avatar search boards (hidden SDK pedestals + search; big catalogues → NekoSune World Avatar Search).
- [x] Photo-spot / camera helper panels.
- [x] Game HUDs (score, timer, lives) for mini-games.
- [x] Admin / moderator panels (owner-only controls, instance-master checks).

Each lesson ends with the user rebuilding a simpler version by hand, so they learn the skill, not just the tool.

### More ready-made pieces
- [x] Settings menu: mirror quality (HQ/LQ/off), music volume, post-processing, pens, chairs, colliders.
- [x] Music player with playlist and shuffle.
- [x] Player list with join time, and a world/instance info card.
- [x] Clock / event countdown.
- [x] Notifications/toasts ("Mirror on", "Welcome!").
- [x] Rules/welcome board with a "don't show again" option (persistence).
- [x] Teleport menu (teleports only move yourself, so no consent step is needed).

## Phase 5 – Later

- [x] Localization: language picker and all 12 languages (same file format as Lip Sync) for the whole UI – 267 strings.
  - [ ] Translate lesson steps, quiz text, glossary and long hints (English for now; they fall back to English automatically).
- [x] Optional ChilloutVR support via reflection: CVR Canvas Wrapper added to new panels, Doctor check, info card. Layout/pages work in CVR; actions and systems are Udon (VRChat only).
- [x] Remote JSON/image data cards (`NekoDataCards`, Remote news board template; also store pictures).
- [x] Video tutorials linked from each lesson (▶ button, `lesson-videos.json`, project override in Assets).
  - [ ] Record the videos and fill in the links.

---

## Unity versions and dependencies

- [x] **Primary target: Unity 2022.3.22f1**, the version VRChat currently requires. Set `"unity": "2022.3"` in `package.json`.
- [x] **Future: Unity 6 (6000.x)**, for when VRChat moves to it. Keep the code compiling there behind version checks instead of a separate branch: compiles with 0 errors / 0 warnings in 6000.3.27f1.
  - [x] `#if UNITY_6000_0_OR_NEWER` for changed APIs (TMP word wrapping); `FindFirstObjectByType` / `FindObjectsByType` everywhere (they exist in 2022.3 too).
  - [x] Avoid editor APIs that are obsolete in Unity 6 (IMGUI is fine; check `EditorGUILayout`/`Handles` deprecations each release).
- [x] **ChilloutVR (Phase 5):** CCK 3 targets Unity 2021.3 and CCK 4 targets 2022.3. If CVR support is added, either lower `"unity"` to 2021.3 and guard newer APIs, or support CCK 4 only. → CCK 4 only (2022.3).
- [x] **VPM dependencies:**
  - [x] `com.vrchat.worlds` (includes UdonSharp) – needed for the generated helper and `VRCUiShape`. Pick the lowest version that has everything used (check `SendCustomEvent`, `VRCUiShape`, persistence if used).
    - Declares `>=3.10.1`: the store and persistence systems need Creator Economy and PlayerData APIs that older SDKs don't have. Compiled (C# + UdonSharp) against 3.10.1 and 3.10.5.
  - [x] Unity `com.unity.ugui` and `com.unity.textmeshpro` (`"dependencies"` in `package.json`).
  - [x] No hard dependency on the NekoSune World Hub or any other NekoSune package.
- [x] **Optional packages detected at runtime** (reflection or asmdef `versionDefines`, never a hard reference): ChilloutVR CCK, TextMeshPro vs legacy Text, Modular Avatar/VRCFury not needed for worlds.
- [x] ~~Use asmdef `versionDefines`…~~ Not needed: com.vrchat.worlds (with UdonSharp) is a required dependency; ChilloutVR, VRCUiShape and mirrors are found by type name instead.
- [x] Show the detected Unity version, VRChat SDK version and UdonSharp state in an **Environment** card, with a warning when the Unity version isn't the one VRChat supports.

## Testing checklist (every phase)

- [x] Compiles in Unity 2022.3.22f1 with the VRChat Worlds SDK and UdonSharp (all 23 Udon programs compile, also for Android).
- [x] Compiles in the newest installed Unity 6 (6000.3.27f1) – 0 errors, 0 warnings from the package.
- [x] Compiles with the lowest supported `com.vrchat.worlds` (3.10.1) and with the latest installed (3.10.5).
- [x] Batch tests: all 29 templates build, Doctor scan, blueprint round trip, QR codes decode (OpenCV), every lesson check runs, translations load.
- [x] Play-mode tests with ClientSim: actions, pages, synced toggle, keypad, poll, scoreboard, store, tabs, admin gate, HUD, dialogue, popup.
- [x] Screenshots of the window (460 px wide) during development.
  - [ ] Screenshots at 400 px and 800 px for every tab (best done by you – the test editor can capture other windows on your screen).
- [ ] Build & Test in VRChat: buttons work on desktop and in VR, laser hits the panel, text readable at 2 m. **Needs you.**
- [x] Quest build check for any generated textures/materials: the test world compiles for Android (C# + UdonSharp); QR textures are capped at 1024 px and the Doctor limits big UI textures for Android.

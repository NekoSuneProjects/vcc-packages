# NekoSune World UI Assistant – TODO

A from-scratch Unity editor tool that helps beginners **build** VRChat world UI visually and **teaches them how it works** over time, so they eventually don't need the tool.

Package: `com.nekosune.world-ui-assistant` · Branch: `world-ui-assistant` · Menu: `NekoSune → World → UI Assistant`

Status: **Phase 1 done – 0.1.0 preview.** Phase 2 (button actions) is next.

---

## Principles

- [ ] **Beginner first.** No Unity UI knowledge assumed. Every screen says what it does and why.
- [ ] **Teach, don't hide.** Everything the tool generates is normal Unity UI the user can open and edit. The tool explains what it made.
- [ ] **Learn by doing.** Short lessons that use the user's own UI, not separate demo scenes.
- [x] **Safe.** Every change is one Undo step. Never edit the user's assets without asking.
- [x] **Standalone, like the other packages.** Own UI, NekoSuneVR footer, update checker, VCC package with release workflow.

## Lessons from the old World UI Builder (`dssdf/.../world-ui-builder`)

Reuse:
- [x] Theme presets (Neko Dark, Light, Neon, Glass, Pastel, Terminal). Custom: edit colours per item for now.
- [ ] JSON blueprint export/import.
- [ ] UI Doctor checks (World Space canvas, GraphicRaycaster, VRCUiShape, Navigation = None, small targets, small text).

Avoid:
- [ ] Wiring buttons with a UnityEvent straight to an UdonSharp method. Use `UdonBehaviour.SendCustomEvent("Name")` instead (the likely bug in World Gallery / Avatar Search Auto-Wire).
- [ ] Encoding actions in GameObject names (`NUI[id|action|value]`). Store them in a small marker component or ScriptableObject.
  - Phase 1 needs no markers: widgets are recognised from their normal Unity components (Button, Toggle, layout groups…), so hand-made UI works too. VRChat only strips `IEditorOnly` components from **avatars**, so a world marker component would ship in the world. For Phase 2 actions, keep the data in the generated Udon helper or a ScriptableObject instead.
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
- [ ] Optional "grow with content" panel (ContentSizeFitter on the panel) – deferred, fixed sizes are easier for beginners to place.
- [x] Live preview: changes show immediately in the Scene view; a **Frame panel** button.
- [x] Panel health line (World Space, GraphicRaycaster, VRCUiShape, UI layer) with one-click fixes – an early piece of the Phase 4 UI Doctor.
- [x] Environment card: warns when Unity isn't 2022.3, the Worlds SDK is missing, or TMP Essentials aren't imported (with an import button).
- [x] "How it works in Unity" line on every Edit card – an early piece of the Phase 3 teaching mode.

## Phase 2 – Make it do things (no code)

- [ ] **Action picker** on buttons and toggles: Show/Hide object, Toggle object, Play/Stop audio, Teleport player, Toggle mirror, Open another page, Send custom event.
- [ ] Generate one readable UdonSharp helper (`NekoUiActions`) into `Assets/NekoSune/UIAssistant/Generated/`; buttons call it through `SendCustomEvent`.
- [ ] Local vs synced choice per action, with a one-line explanation of what "synced" means.
- [ ] **Pages:** tabs/sub-menus with back buttons, built from the outline.
- [ ] Check after building: every action target exists and the helper compiled (UdonSharp program asset repair, as in Avatar Search).

## Phase 3 – Teaching mode (long-term learning)

- [ ] **Explain mode toggle:** hovering a field shows *what Unity component this changes* (e.g. "this is `RectTransform.sizeDelta`").
- [ ] **"What did it make?" panel:** lists every generated component with a short explanation and a Select button.
- [ ] **Lessons** (5–10 min each, use the user's own panel, step-by-step with highlights and a "check my work" button):
  1. What a Canvas is and why worlds use World Space
  2. RectTransform: anchors, pivot, size
  3. Layout groups and content size fitters
  4. Buttons, events and why VRChat needs `SendCustomEvent`
  5. Udon basics: reading the generated helper script
  6. Syncing: local vs networked UI
  7. Performance: overdraw, canvas count, texture sizes, Quest limits
  8. Building a UI by hand without the Assistant (graduation)
- [ ] **Progress tracking** per machine (EditorPrefs): lessons done, badges, "next suggested lesson".
- [ ] **Hint level setting:** Full help → Some help → Expert (hides explanations as the user improves).
- [ ] Glossary page with every term used in the tool.

## Phase 4 – Doctor and polish

- [ ] UI Doctor with one-click fixes (from the old builder, extended): missing raycaster, wrong canvas mode, missing VRCUiShape, Navigation on, tiny buttons, unreadable text, overlapping panels, Quest-heavy textures.
- [ ] Templates: Welcome board, Rules, Settings (mirror/audio/post-processing), Teleport menu, Credits, Event schedule, Links/QR.
- [ ] JSON blueprint export/import and share.
- [ ] Undo coverage for every action; no lost work on script reload.

## Phase 4b – Ready-made UI systems

Each one is a template the user drops in and customises with the same editor, and each has its own lesson.

### Video player UI
- [ ] Control panel for a video player: Play/Pause, Stop, seek bar, time, volume, URL field, Sync button, Loop toggle.
- [ ] Works with the stock VRChat players (AVPro / Unity Video) through the generated helper.
- [ ] Detects popular community players (ProTV, USharpVideo, VideoTXL, YamaPlayer, VizVid…) by reflection and links to their existing controls instead of replacing their sync logic (reuse the detection from World YouTube Proxy).
- [ ] Playlist page (predeclared `VRCUrl[]`) with titles and thumbnails.
- [ ] Owner/master-only lock option, explained in plain English.
- [ ] Lesson: how VRChat video players sync, and why URLs must be `VRCUrl`.

### Portable UI (follows the player)
- [ ] Menu that opens near the player instead of being fixed in the world: in front of the face, on the wrist, or from the hand.
- [ ] Open/close by button, gesture (VR) or hotkey (desktop); remembers the last page.
- [ ] Follow modes: stays where opened, smoothly follows the head, or attaches to a hand.
- [ ] Distance, scale and auto-hide timer settings; local-only by default (each player sees only their own).
- [ ] VR and desktop tested separately (laser reach, readable size, doesn't block the view).
- [ ] Lesson: tracking data (`GetTrackingData`) and why portable UI is local-only.

### Hotkey toggles
- [ ] Bind desktop keys (e.g. `M` mirror, `P` portable menu, `F1` help) to any UI action.
- [ ] VR equivalents via VRChat input events (`InputUse`, `InputJump`, `InputLookVertical`…), with a note on what VRChat doesn't allow.
- [ ] On-screen hint list of the world's hotkeys, toggleable by the player.
- [ ] Conflict check against VRChat's own desktop keys.
- [ ] Lesson: Udon input events vs `Input.GetKeyDown`.

### Store / market UI
- [ ] Catalog builder: grid or list of product cards (image, name, price/tier, short description, badge such as "New"/"Owned").
- [ ] Product detail page: large image gallery, **video preview** (reuses the video player UI), long description, buy/info button.
- [ ] **Links attached to products.** Be honest about VRChat limits and pick the right option for each link:
  - [ ] VRChat Creator Economy items: open the real store with `Store.OpenListing` / `Store.OpenWorldStorePage`, unlock content with `UdonProduct` ownership (reuse the World Economy lifecycle notes: wait for `OnPurchasesLoaded`, etc.).
  - [ ] External shops (Booth, Gumroad, Jinxxy, Patreon…): Udon can't open a browser, so show the URL as text plus a generated **QR code** texture, and a copy-able text field.
- [ ] Categories, filters and search (text field), sort by price/new.
- [ ] Data from a JSON file or remote JSON feed (reuse World Data / Gallery mapping), so the creator updates the shop without re-uploading the world. Images from predeclared `VRCUrl`s.
- [ ] "Owned" state and unlocking world content for buyers (Creator Economy only).
- [ ] Supporter / thank-you wall linked to the store.
- [ ] Lessons: how VRChat purchases work, why external links need QR codes, and keeping a shop updatable with remote data.

### Everything else VRChat UI can do (coverage list)
Each gets a template **and** a lesson, so the Assistant covers the whole range of VRChat world UI.

- [ ] Menus, tabs and sub-pages (core).
- [ ] Forms and input: text fields, keypads, password/code doors.
- [ ] Scoreboards and leaderboards (synced and persistent).
- [ ] Polls and voting (synced, one vote per player).
- [ ] Dialogue / NPC conversation boxes.
- [ ] Maps and "you are here" boards with teleport points.
- [ ] Inventory, achievements and progress screens (persistence).
- [ ] Image galleries and slideshows (reuse World Gallery).
- [ ] Avatar pedestals and avatar search boards (reuse World Avatar Search).
- [ ] Photo-spot / camera helper panels.
- [ ] Game HUDs (score, timer, lives) for mini-games.
- [ ] Admin / moderator panels (owner-only controls, instance-master checks).

Each lesson ends with the user rebuilding a simpler version by hand, so they learn the skill, not just the tool.

### More ready-made pieces
- [ ] Settings menu: mirror quality (HQ/LQ/off), music volume, post-processing, pens, chairs, colliders.
- [ ] Music player with playlist and shuffle.
- [ ] Player list with join time, and a world/instance info card.
- [ ] Clock / event countdown.
- [ ] Notifications/toasts ("Mirror on", "Welcome!").
- [ ] Rules/welcome board with a "don't show again" option (persistence).
- [ ] Teleport menu (reuse the World Player Tools consent model).

## Phase 5 – Later

- [ ] Localization: language picker and all 12 languages (same system as Lip Sync).
- [ ] Optional ChilloutVR support (CVR Canvas Wrapper) via reflection.
- [ ] Remote JSON/image data cards (reuse World Data / Gallery ideas).
- [ ] Video tutorials linked from each lesson.

---

## Unity versions and dependencies

- [x] **Primary target: Unity 2022.3.22f1**, the version VRChat currently requires. Set `"unity": "2022.3"` in `package.json`.
- [ ] **Future: Unity 6 (6000.x)**, for when VRChat moves to it. Keep the code compiling there behind version checks instead of a separate branch:
  - [ ] `#if UNITY_6000_0_OR_NEWER` for changed APIs (e.g. `FindObjectsOfType` → `FindObjectsByType`, `Object.FindFirstObjectByType`).
  - [ ] Avoid editor APIs that are obsolete in Unity 6 (IMGUI is fine; check `EditorGUILayout`/`Handles` deprecations each release).
- [ ] **ChilloutVR (Phase 5):** CCK 3 targets Unity 2021.3 and CCK 4 targets 2022.3. If CVR support is added, either lower `"unity"` to 2021.3 and guard newer APIs, or support CCK 4 only.
- [ ] **VPM dependencies:**
  - [ ] `com.vrchat.worlds` (includes UdonSharp) – needed for the generated helper and `VRCUiShape`. Pick the lowest version that has everything used (check `SendCustomEvent`, `VRCUiShape`, persistence if used).
    - 0.1.0 declares `>=3.5.0` (Phase 1 only uses `VRCUiShape`, found by name). Tested with 3.10.5; the lowest version is not verified yet.
  - [x] Unity `com.unity.ugui` and `com.unity.textmeshpro` (`"dependencies"` in `package.json`).
  - [x] No hard dependency on the NekoSune World Hub or any other NekoSune package.
- [ ] **Optional packages detected at runtime** (reflection or asmdef `versionDefines`, never a hard reference): ChilloutVR CCK, TextMeshPro vs legacy Text, Modular Avatar/VRCFury not needed for worlds.
- [ ] Use asmdef `versionDefines` to light up features only when a package is installed, e.g. `NEKO_HAS_UDONSHARP` when `com.vrchat.worlds` is present.
- [x] (partly) Show the detected Unity version, VRChat SDK version and UdonSharp state in an **Environment** card, with a warning when the Unity version isn't the one VRChat supports.

## Testing checklist (every phase)

- [ ] Compiles in Unity 2022.3.22f1 with the VRChat Worlds SDK and UdonSharp.
- [ ] Compiles in the newest installed Unity 6 (6000.x) editor, even if VRChat doesn't use it yet.
- [ ] Compiles with the lowest supported `com.vrchat.worlds` version and with the latest.
- [ ] Screenshot of each window at 400 px and 800 px width.
- [ ] Build & Test in VRChat: buttons work on desktop and in VR, laser hits the panel, text readable at 2 m.
- [ ] Quest build check for any generated textures/materials.

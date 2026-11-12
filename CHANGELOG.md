# Changelog

## 0.4.0 – ready-made systems (Phase 4b of TODO.md)

21 new templates, each built from normal UI plus one small, commented UdonSharp script, each with a lesson that ends in a hand-built version:

- **Media:** Video player controls (stock AVPro/Unity players, synced with server time, URL field, playlist, loop, resync, volume; adds the SDK player if your scene has none; detects ProTV, USharpVideo, VideoTXL, YamaPlayer and VizVid and leaves their sync alone), Music player (playlist, shuffle, volume, local or synced), Gallery/slideshow.
- **Menus:** Tabbed menu, Portable menu (in front of you, follows your head or sits on a hand; desktop key or VR double-tap; auto-hide), Hotkeys (desktop keys for any action, on-screen list on F1, VR double-jump, VRChat key-conflict warnings), Admin panel (instance master / owner / name list).
- **Shop:** Store/market – product cards, search, categories, sort by price/new/name, detail page with picture and video preview, VRChat Creator Economy listings (Store.OpenListing, Owned badges, unlocks for buyers via OnPurchasesLoaded/OnPurchaseConfirmed), external shops with QR codes and copyable links, optional remote JSON data, supporter wall.
- **Games & social:** Keypad/code door, Scoreboard (synced, plus personal best saved with PlayerData), Game HUD (score, lives, timer, game over), Achievements/progress (PlayerData), Poll (one vote per player), Dialogue box, Player list & instance info, Avatar board (hidden pedestals + search).
- **Pieces:** Map "you are here", Photo spot helper (3-2-1 + ring light), Clock & event countdown (UTC), Message popup, Welcome with "Don't show again" (PlayerData).
- **System settings** card in the Build tab for the scripts behind a template.
- Tested in Play mode with ClientSim: keypad + door, poll, scoreboard, store search/detail/sort, tabs, admin gate, HUD, dialogue, popup.
- Fixed: plain columns directly under a panel were treated as pages; templates sized texts without a Layout Element.

## 0.3.0 – teaching mode, Doctor and templates (Phases 3 and 4 of TODO.md)

- **Tabs:** Build, Learn, Doctor, Templates and Glossary.
- **Lessons:** 8 core lessons (Canvas, RectTransform, layout, events and SendCustomEvent, reading the Udon script, syncing, performance, graduation) plus one per template. Steps use your own panel, highlight the card to look at, have "Show me" helpers, "Check my work" checks and quiz questions. Each template lesson ends with you rebuilding a simpler version by hand.
- **Progress** per computer: lessons done, badges and the next suggested lesson.
- **Help level:** Full help → Some help → Expert, with a suggestion to lower it as you finish lessons.
- **Explain mode:** shows which Unity component and property each setting changes.
- **What did it make?** card listing every component in the panel with a short explanation and Select buttons.
- **Glossary** with search (45 terms).
- **UI Doctor:** World Space, Graphic Raycaster, VRCUiShape, UI layer, Navigation, small click targets, small or invisible text, decorations that catch laser hits, Quest-heavy textures (Android size limit fix), overlapping panels, visible pages, action problems. One-click fixes and Fix all, all undoable.
- **Templates:** Welcome board (with Rules/Credits pages), Rules, Settings (mirror HQ/LQ/Off, music volume, post-processing, pens, chairs, colliders, status message), Teleport menu (points created for you), Credits, Event schedule, Links/QR.
- **QR code maker:** turns links into scannable sprites (Udon can't open web pages).
- **Blueprints:** export/import a panel as JSON (file or clipboard), including actions.
- New actions: **Show one of a group** (e.g. mirror HQ/LQ/Off) and an optional **message popup** after any action (`NekoToast`).
- **Slider actions** ("When it's moved"): sound volume, Animator float, or send the value to Udon (`NekoUiSlider`), local or synced.
- New panels move aside instead of overlapping existing ones.
- ChilloutVR: the CVR Canvas Wrapper is added when the CCK is installed (found by name).

## 0.2.0 – actions and pages (Phase 2 of TODO.md)

- **Actions** on buttons and toggles ("When it's clicked" in the Edit card): show something, hide something, show/hide, turn a mirror on/off, play or stop a sound, teleport the player, open a page, or send an event to your own Udon script.
- **Local or synced** per action, with a plain-English explanation. Synced state also reaches players who join later.
- Toggles drive their action from their on/off state (e.g. toggle on = object shown).
- **Pages:** the new Page item adds a page with a heading and a working Back button. The first time, everything on the panel becomes the Home page. Show any page while editing; only one is visible at a time.
- Actions run through one small, commented UdonSharp script shipped with the package (`Runtime/NekoUiAction.cs`). The button's On Click calls `UdonBehaviour.SendCustomEvent("Run")`, the way VRChat UI has to call Udon.
- **Action check** on the panel card: missing objects, sounds, teleport points or pages, unconnected buttons, out-of-date page lists (one-click fix) and UdonSharp compile errors.
- New teleport points can be made at the centre of the Scene view.
- Fixed: the panel list could throw errors after leaving Play mode.

## 0.1.0 – first preview (Phase 1 of TODO.md)

- **New panel wizard:** name, size (small sign, menu, big board), theme and placement (in front of the Scene view camera, at the world origin, or inside the selected object). Creates a VRChat-ready world canvas: World Space, 1 UI unit = 1 mm, GraphicRaycaster, VRCUiShape, UI layer, plus an EventSystem for editor testing.
- **Themes:** Neko Dark, Light, Neon, Glass, Pastel and Terminal. Click a theme on a panel to re-colour everything in one Undo step.
- **Add palette:** Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Row, Grid and Scroll list. New items go after the selected item, or inside a selected Row, Grid or Scroll list.
- **Outline:** the panel's items top to bottom. Click to select (also in the Hierarchy), drag to reorder or drop into a Row/Grid/Scroll list, show/hide, duplicate, delete.
- **Edit card** per item with plain-English settings and a "How it works in Unity" line naming the components behind it.
- **Panel card:** size, background, padding, gap, theme, Frame in Scene view, and a VRChat-ready check with one-click fixes.
- Everything is ordinary Unity UI (TextMeshPro text, layout groups). Nothing extra is added to your objects, and every change can be undone.
- Update checker and NekoSuneVR footer.

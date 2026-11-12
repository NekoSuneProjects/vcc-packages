# Changelog

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

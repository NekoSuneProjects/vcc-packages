# Changelog

## 0.1.0 – first preview (Phase 1 of TODO.md)

- **New panel wizard:** name, size (small sign, menu, big board), theme and placement (in front of the Scene view camera, at the world origin, or inside the selected object). Creates a VRChat-ready world canvas: World Space, 1 UI unit = 1 mm, GraphicRaycaster, VRCUiShape, UI layer, plus an EventSystem for editor testing.
- **Themes:** Neko Dark, Light, Neon, Glass, Pastel and Terminal. Click a theme on a panel to re-colour everything in one Undo step.
- **Add palette:** Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Row, Grid and Scroll list. New items go after the selected item, or inside a selected Row, Grid or Scroll list.
- **Outline:** the panel's items top to bottom. Click to select (also in the Hierarchy), drag to reorder or drop into a Row/Grid/Scroll list, show/hide, duplicate, delete.
- **Edit card** per item with plain-English settings and a "How it works in Unity" line naming the components behind it.
- **Panel card:** size, background, padding, gap, theme, Frame in Scene view, and a VRChat-ready check with one-click fixes.
- Everything is ordinary Unity UI (TextMeshPro text, layout groups). Nothing extra is added to your objects, and every change can be undone.
- Update checker and NekoSuneVR footer.

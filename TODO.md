# NekoSune World UI Assistant – TODO

A from-scratch Unity editor tool that helps beginners **build** VRChat world UI visually and **teaches them how it works** over time, so they eventually don't need the tool.

Package: `com.nekosune.world-ui-assistant` · Branch: `world-ui-assistant` · Menu: `NekoSune → World → UI Assistant`

Status: planning only. No code yet.

---

## Principles

- [ ] **Beginner first.** No Unity UI knowledge assumed. Every screen says what it does and why.
- [ ] **Teach, don't hide.** Everything the tool generates is normal Unity UI the user can open and edit. The tool explains what it made.
- [ ] **Learn by doing.** Short lessons that use the user's own UI, not separate demo scenes.
- [ ] **Safe.** Every change is one Undo step. Never edit the user's assets without asking.
- [ ] **Standalone, like the other packages.** Own UI, NekoSuneVR footer, update checker, VCC package with release workflow.

## Lessons from the old World UI Builder (`dssdf/.../world-ui-builder`)

Reuse:
- [ ] Theme presets (Neko Dark, Light, Neon, Glass, Pastel, Terminal, Custom).
- [ ] JSON blueprint export/import.
- [ ] UI Doctor checks (World Space canvas, GraphicRaycaster, VRCUiShape, Navigation = None, small targets, small text).

Avoid:
- [ ] Wiring buttons with a UnityEvent straight to an UdonSharp method. Use `UdonBehaviour.SendCustomEvent("Name")` instead (the likely bug in World Gallery / Avatar Search Auto-Wire).
- [ ] Encoding actions in GameObject names (`NUI[id|action|value]`). Store them in a small marker component or ScriptableObject.
- [ ] A static-only window that loses the user's work on script reload. Keep state in a serialized asset.

---

## Phase 1 – MVP: build a panel without knowing Unity UI

- [ ] Package skeleton: `package.json`, asmdef, README, CHANGELOG, `.meta` files, release workflow with the 3-way version check, `version.json`.
- [ ] Update checker + NekoSuneVR footer (copy from World Gallery).
- [ ] **New panel wizard:** pick a size (small sign, menu, big board), a theme and a place in the scene (in front of the Scene view camera).
- [ ] Generate a correct VRChat world canvas: World Space, sensible scale (1 px ≈ 1 mm), `GraphicRaycaster`, `VRCUiShape`, UI layer, Navigation = None.
- [ ] **Widget palette:** Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Scroll list. Add with one click.
- [ ] **Outline list:** reorder (drag), rename, duplicate, delete. Clicking selects the real object.
- [ ] **Inspector card per widget:** text, size, colours, font size, with plain-English labels and tooltips.
- [ ] Layout handled for them: VerticalLayoutGroup + ContentSizeFitter by default; "rows" and "grids" as simple choices.
- [ ] Live preview: changes show immediately in the Scene view; a **Frame panel** button.

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

## Phase 5 – Later

- [ ] Localization: language picker and all 12 languages (same system as Lip Sync).
- [ ] Optional ChilloutVR support (CVR Canvas Wrapper) via reflection.
- [ ] Remote JSON/image data cards (reuse World Data / Gallery ideas).
- [ ] Video tutorials linked from each lesson.

---

## Testing checklist (every phase)

- [ ] Compiles in Unity 2022.3.22f1 with the VRChat Worlds SDK and UdonSharp.
- [ ] Screenshot of each window at 400 px and 800 px width.
- [ ] Build & Test in VRChat: buttons work on desktop and in VR, laser hits the panel, text readable at 2 m.
- [ ] Quest build check for any generated textures/materials.

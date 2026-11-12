# Changelog

## 1.1.0

- New **Frames** tab (Frame Calculator): pick a clip and your recording FPS (24 / 30 / 60 / 120 presets or any value) to get the recorder start frame, end frame and frame count, with copy buttons, timecode and clip details.
- Also opens from **Tools → NekoSuneVR → Unity Essentials → Frame Calculator**.
- Installing removes the old loose script `Assets/[NekoAnimationSetup]/[scripts]/AnimationFrameCalculator.cs` (old menu: Tools → Animation Frame Calculator).
- Shorter tab names (Organizer, Toolkit, Studio, Frames) so all four fit.

## 1.0.0

- First release as a VCC/VPM package (`com.nekosune.unity-essentials`).
- New Unity Essentials window (**Tools → NekoSuneVR → Unity Essentials**) with one tab per tool: Organizer, Animation Toolkit and Animation Studio.
- Organizer: visual rule editor with a live preview of each Hierarchy/folder rule, add/remove/reorder, undo support, and quick actions.
- Animation Toolkit and Animation Studio: card layout, buttons that only enable when their inputs are set, and a result line after each action.
- In-editor update checker: version chip in the header (amber when an update is out) and an update banner with Download / Later / Skip this version.
- NekoSuneVR footer.
- Installing the package removes the old copy in `Assets/NekoUnityEssentials` or `Assets/[NekoAnimationSetup]/[scripts]/NekoUnityEssentials`. Your rules in `Assets/Editor/NekoUnityEssentialsSettings.asset` are kept.

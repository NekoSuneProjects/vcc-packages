# Changelog

## 1.0.0

- First standalone release as a VCC/VPM package (`com.nekosune.lipsync`). No NekoSune Avatar Hub required.
- Bakes VRChat viseme blendshapes, a jaw bone or a single mouth-open blendshape from any AudioClip.
- Binds from the VRChat Avatar Descriptor, or auto-detects visemes by name (VRChat, VRoid/MMD kana, CATS, ARKit).
- Built-in presets (Song, Speech, Anime, Subtle, Noisy recording) plus savable user presets.
- 12 UI languages.
- Fixed: saved presets losing their script reference after Unity reloads (`NekoLipSyncPreset` now has its own file).

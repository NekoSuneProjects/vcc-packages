# Changelog

## 1.1.0

- Redesigned window in the Motion Converter style: header with title, version chip and language picker; bordered cards for Avatar & audio, Preset, Settings, Targets, Avatar binding and Output; big accent "Make lip sync" button and a result row with Ping / Select.
- In-editor update checker (same as Motion Converter): checks version.json at most every 6 hours, clickable version chip, update banner with Download / Later / Skip this version, one Console notice per new version.
- Added the NekoSuneVR footer from Motion Converter (badge, tagline and social links).
- All new text translated into all 12 languages.

## 1.0.0

- First standalone release as a VCC/VPM package (`com.nekosune.lipsync`). No NekoSune Avatar Hub required.
- Bakes VRChat viseme blendshapes, a jaw bone or a single mouth-open blendshape from any AudioClip.
- Binds from the VRChat Avatar Descriptor, or auto-detects visemes by name (VRChat, VRoid/MMD kana, CATS, ARKit).
- Built-in presets (Song, Speech, Anime, Subtle, Noisy recording) plus savable user presets.
- 12 UI languages.
- Fixed: saved presets losing their script reference after Unity reloads (`NekoLipSyncPreset` now has its own file).

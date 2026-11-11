# Changelog

## 1.0.0

- First release as a VCC/VPM package (`com.nekosune.eye-installer`).
- Blender-powered eye replacement: detect the face mesh and eye bones, remove the old eyes (by bone weights, placement spheres or eye materials) and build new sphere or flat eyes skinned to the rig.
- Draggable eye placement in the Scene view, with left-to-right mirroring, for rigs without eye bones.
- Bundled `NekoSuneVR/Eye` shader; downloads a portable Blender next to the project when needed.
- In-editor update checker: clickable version badge in the header and an update banner (Download / Later / Skip this version).
- NekoSuneVR footer with social links (Twitch, X, YouTube, Bluesky, Discord, Patreon, Ko-fi).
- Installing the package removes the old script copy from `Assets/NekoSuneVR/NekoEyeInstaller` (Editor and Shaders folders only). Generated eyes in `Assets/NekoSuneVR/NekoEyeInstaller/Generated` are kept, and existing eye materials keep their shader.

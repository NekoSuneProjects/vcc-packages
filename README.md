# NekoSune Unity Essentials

Colour and badge your Hierarchy and Project folders by name, plus non-destructive animation tools for `.anim` clips, all in one window.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Unity Essentials** (`com.nekosune.unity-essentials`) to your project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you had the tool in `Assets/NekoUnityEssentials` or `Assets/[NekoAnimationSetup]/[scripts]/NekoUnityEssentials`, installing the package removes that old copy, and the old loose `Assets/[NekoAnimationSetup]/[scripts]/AnimationFrameCalculator.cs` script. Your rules (`Assets/Editor/NekoUnityEssentialsSettings.asset`) are kept.

## Use it

Open **Tools → NekoSuneVR → Unity Essentials**. The window has four tabs:

- **Organizer** – click **Create Default Settings** once, then add Hierarchy and folder rules: name to match (contains / starts with / exact), row colour, badge, icon, priority and "colour children too". Each rule shows a live preview. Quick actions group the selected objects or create standard folders.
- **Toolkit** (Animation Toolkit) – create a clip, write start → end position / rotation / scale keys for a bone or object, or duplicate a clip with renamed binding paths.
- **Studio** (Animation Studio) – duplicate, reverse, loop / ping-pong and simplify clips; create on/off toggle and blendshape clips; swap Left/Right paths; find missing paths; build an Animator Controller from selected clips.
- **Frames** – Frame Calculator: pick a clip and your recording FPS to get the recorder start frame, end frame and frame count (with copy buttons), plus the clip's duration, timecode and frame rate.

Right-click menus: **GameObject → Neko Essentials → Group Selected Objects** and **Assets → Neko Essentials → Create Standard Folders**.

Processing tools always write a new `.anim` next to the source; originals and FBX clips are never changed. Folder styling only changes the Project window, not folders on disk.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `unity-essentials` branch. If a newer one is out, the version chip in the header turns amber (`v1.0.0 → v1.1.0`) and a banner offers **Download update**, **Later** and **Skip this version**. Click the chip to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `unity-essentials` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

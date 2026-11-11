# NekoSune Eye Installer

Replace an avatar's eyes with clean, properly skinned eye meshes. The geometry work happens in Blender, driven from Unity: pick the avatar, press **Detect Eyes**, press **Install Eyes**.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Eye Installer** (`com.nekosune.eye-installer`) to your project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you previously had the tool in `Assets/NekoSuneVR/NekoEyeInstaller`, installing the package removes the old `Editor` and `Shaders` folders. Your generated eyes in `Generated/` stay, and existing eye materials keep working.

## Open it

**Tools → NekoSuneVR → Eye Installer**

## How it works

1. **Blender** – Blender 4.0.2 is required. If it isn't found, the window can download a portable copy next to your project (outside `Assets`, so Unity never imports it), or point it at your own install with **Set Path**.
2. **Avatar** – pick the avatar root. The face mesh and eye bones are detected from the humanoid rig; override them if needed.
3. **Placement** – measured from the eye bones, or placed by hand with draggable Scene-view handles (needed for rigs without eye bones).
4. **Eye Shape** – Sphere or Flat, size, depth, iris coverage and mesh density.
5. **Detection** – choose how the old eyes are removed: by bone weights, placement spheres or eye materials, or keep them. **Detect Eyes** previews without changing anything.
6. **Eye Material** – use your own, or let it generate one from the bundled `NekoSuneVR/Eye` shader with your iris texture.
7. **Install Eyes** – writes the edited mesh to `Assets/NekoSuneVR/NekoEyeInstaller/Generated/<Avatar>/` and swaps it in as a single undo step.

The source FBX is read, never written. **Remove Installed Eyes** undoes an install.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `eye-installer` branch. If a newer one is out, the version badge in the header turns amber (`v1.0.0 → v1.1.0`), a banner offers **Download update**, **Later** and **Skip this version**, and one warning is written to the Console. Click the badge to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `eye-installer` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

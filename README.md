# NekoSuneVR VRChat Intro + Idle Audio Installer

A Unity Editor installer for VRChat avatars that builds a spawn/load intro and an AFK idle-audio sequence, then merges it through either **VRCFury** or **Modular Avatar**.

## What it builds

### Avatar load intro
When the avatar FX animator initializes:

1. Intro state starts immediately.
2. Optional intro particles turn on.
3. Optional Doppelganger Dope ScreenFX turns on.
4. An intro sound plays.
   - Assign your own clip, or
   - leave it empty and the installer generates an original digital boot/chime WAV automatically.
5. After the configured intro duration, everything turns off and the avatar enters its normal state.

The intro can replay when the avatar/FX animator is reloaded, such as swapping back to the avatar.

### AFK idle sequence
When VRChat's built-in `AFK` parameter becomes true:

1. AFK loading particles turn on.
2. Optional Doppel ScreenFX loading preset turns on.
3. After the configured loading duration, the loading effects turn off.
4. The chosen AFK idle audio begins.
5. Leaving AFK stops the audio and returns to Normal immediately.

The installer uses VRChat's built-in `AFK` animator parameter, so it does **not** add an expression parameter cost for AFK detection.

## Doppelganger Dope Shader 2.3.5 integration

The installer was updated after inspecting the supplied `Doppelganger_Patreon_Dope_Shader_2.3.5.unitypackage`.

The package's own `DopeMenuItem.cs` creates these hierarchy templates:

- `GameObject > DopeSFX > Particle System`
- `GameObject > DopeSFX > Skinned Mesh Renderer`
- `GameObject > DopeSFX > Depth Light`

The installer mirrors that setup automatically.

For **Particle System** mode it also mirrors Doppel's own **Configure Particle Systems Renderer** code from `DopeSFXGUI235.cs`:

- active vertex streams: `Position`, `Color`, `Center`
- renderer mode: `Mesh`
- mesh: Unity built-in `Cube.fbx`
- Dope material property `UFPS = 1`

The generated material uses the installed shader named like:

`Doppels shaders/ScreenFX/Dope Shader 2.3.5`

The installer has two built-in material presets:

- **DigitalBoot** — lighter RGB glitch, lines, small zoom/shake, film grain, noise and vignette for avatar spawn.
- **AFKLoading** — stronger glitch, RGB split, light pixelation/waves, film grain, noise, lines and vignette while entering AFK.

You can switch the auto-created renderer between Doppel's **Particle System** and **Skinned Mesh Renderer** template.

There is also an optional **Depth Enabler Light**, but it is disabled by default because the included presets do not depend on depth effects.

### Paid asset safety

This installer does **not** contain, copy, or redistribute Doppelganger's shader/package files. The user must already have the Doppel package installed in their own Unity project. The installer only detects the installed shader/template prefabs and creates references/materials using them.

If you prefer your own Doppel setup, assign a dedicated prefab or existing avatar object to **Custom Doppel Override** and automatic setup will not modify it.

## Requirements

- VRChat Creator Companion avatar project / supported Unity version
- VRChat Avatars SDK
- One of:
  - VRCFury, or
  - Modular Avatar
- Optional: Doppelganger Dope Shader for automatic ScreenFX integration

## Installation

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Intro + Idle Audio** (`com.nekosune.intro-idle-audio`) to your avatar project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you previously had the tool in `Assets/NekoSuneVR/VRChatIdleAudioInstaller`, installing the package removes that old copy. Your generated output in `Assets/NekoSuneVR/Generated/IntroIdleAudio` is kept, and existing particle materials keep their shader.

Then open:

- `Tools > NekoSuneVR > Intro + Idle Audio` – the hub, with avatar and environment checks
- `Tools > NekoSuneVR > Avatar Intro Installer`
- `Tools > NekoSuneVR > AFK Idle Audio Installer`

## Easy setup

1. Select the avatar root containing `VRC Avatar Descriptor`.
2. Choose **VRCFury** or **Modular Avatar**.
3. Leave **Enable Intro** on.
4. Leave Intro Audio empty if you want the generated NekoSune digital boot chime.
5. Choose your AFK idle audio clip.
6. Leave **Auto Setup Doppel** enabled if Doppel is installed.
7. Choose Particle System or Skinned Mesh Renderer for Doppel.
8. Click **Install / Rebuild Intro + Idle Audio**.

The generated assets are stored under:

`Assets/NekoSuneVR/Generated/IntroIdleAudio/<AvatarName>/`

This includes the FX controller, animation clips, generated Doppel materials, and the generated boot-chime WAV when used.

## Doppel detection

Use **Show Doppel Detection** in the installer window to see whether it found:

- the Dope ScreenFX shader
- `[PREFAB] SFX Particle System`
- `[PREFAB] SFX Skinned Mesh Renderer`
- `[PREFAB] Depth Enabler Light`

## Notes

- Use a dedicated custom Doppel object if you choose the override field, because the installer animates its active state.
- Screen shaders can be blocked by a user's VRChat safety/performance settings.
- Test on a duplicate/test avatar before publishing.
- Re-running the installer removes its previous generated avatar root and generated output folder, then rebuilds them.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `intro-idle-audio` branch. If a newer one is out, the version badge in each window's header turns amber (`v2.0.0 → v2.1.0`), a banner offers **Download update**, **Later** and **Skip this version**, and one warning is written to the Console. Click the badge to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `intro-idle-audio` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

## Version 1.1.0

- Added avatar-load intro state.
- Added generated original boot/chime audio.
- Added separate intro particles.
- Added automatic Doppel Dope Shader setup based on the package's actual example/template code.
- Added DigitalBoot and AFKLoading Doppel material presets.
- Added optional Doppel Depth Enabler Light.
- Added Doppel package detection window.
- Keeps VRCFury and Modular Avatar backends.

# NekoSune Lip Sync Studio

Bake lip sync for a VRChat avatar from any audio clip: a song, a voice line, anything Unity imports as an `AudioClip`. The result is a normal `.anim` you can drop into an Animator.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Lip Sync Studio** (`com.nekosune.lipsync`) to your project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

## Open it

- **NekoSune → Avatar → Lip Sync Studio**
- Right-click an AudioClip → **NekoSune → Lip Sync from this audio**
- Right-click an avatar in the Hierarchy → **NekoSune → Lip Sync Studio**

The window works top to bottom, one card per step:

1. **Avatar & audio** – drop in the avatar and the audio clip. Chips show the binding and clip info; the waveform has Play / Stop and shades the parts outside Start / End.
2. **Preset** – a built-in or saved preset.
3. **Settings** – grouped into Feel, Timing, Bake and Range.
4. **Targets** – visemes, jaw bone and/or a single mouth-open shape.
5. **Avatar binding** – which blendshape each viseme uses (green = mapped, red = missing).
6. **Output** – folder and the exact file the clip will be saved as (default `Assets/NekoSune/LipSync`).

Then press **Make lip sync**. The result row has **Ping** / **Select** buttons for the new clip.

## Targets

- **VRChat visemes**: all 15 viseme blendshapes, taken from the avatar's VRC Avatar Descriptor, or auto-detected by name (VRChat, VRoid/MMD kana, CATS, ARKit-style names).
- **Jaw bone**: rotates the jaw on a chosen axis by how open the mouth is.
- **Single mouth-open blendshape**: one shape driven by mouth openness.
- **Auto**: visemes when available, otherwise jaw, otherwise a single shape.

The VRChat SDK is read through reflection, so the tool also works in projects without it (name-based detection only).

## Settings

Presets: **Default, Song / music, Speech, Anime (exaggerated), Subtle, Noisy recording**. Save your own with **Save preset**; they appear in the list marked ★.

Every setting has a tooltip. The main ones:

- **Volume → mouth** – how much loudness drives how far the mouth opens.
- **Clarity** – commit to one clear viseme per frame instead of blending several.
- **Offset, ms** – shift the whole animation; negative moves the mouth earlier.
- **Attack / Release, ms** – how fast the mouth reaches and leaves a shape.
- **Clean vocal** – turn off music suppression for isolated voice recordings.
- **Start / End** – bake only part of the clip.
- **Key reduction** – removes redundant keys while staying within the tolerance.

## Languages

English, Deutsch, Español, Français, Italiano, 日本語, 한국어, Polski, Português (Brasil), Русский, Українська, 简体中文. Pick one from the dropdown in the window; missing translations fall back to English. Languages are JSON files in `Editor/Localization/Languages`, so adding one is adding a file.

## Updates

Like Motion Converter, the tool checks for a newer version: when Unity loads (at most every 6 hours) it downloads `version.json` from the `lipsync` branch and compares it with the installed version. If a newer one is out:

- the version chip in the header turns orange (`v1.1.0 → v1.2.0`),
- a banner at the top offers **Download update**, **Later** (hide until Unity restarts) and **Skip this version**,
- one warning is written to the Console.

Click the version chip to check right away. Offline or unreachable: nothing is shown, the tool works as normal.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `lipsync` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

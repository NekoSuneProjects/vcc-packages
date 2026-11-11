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

Drop in an avatar and an audio clip, pick a preset and press **Make lip sync**. The clip is saved to `Assets/NekoSune/LipSync` (changeable under *Targets and output*).

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

## Releasing an update

Bump `version` in `package.json`, add a `CHANGELOG.md` entry and push to the `lipsync` branch. The release workflow builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

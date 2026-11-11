# NekoSune Beat Keyframing

An audio timeline for keyframing to music. It shows a waveform plus one lane per frequency band, so the sub/bass energy that drives a drop is visible at a glance, marks detected drops you can jump between, and lines up with Unity's Animation window so its playhead matches your dope sheet.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Beat Keyframing** (`com.nekosune.beat-keyframing`) to your project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you previously had the tool in `Assets/NekoSuneVR/NekoBeatKeyframing`, installing the package removes the old `Editor` folder only. Audio you imported through the tool (`ImportedAudio/`) is kept.

## Use it

1. Open **Tools → NekoSuneVR → Beat Keyframing** (dock it under the Animation window).
2. Drag an audio file onto the timeline: mp3, ogg, wav or aiff, from the Project window or straight from Explorer (Explorer files are copied into `Assets/NekoSuneVR/NekoBeatKeyframing/ImportedAudio`).
3. With **Follow Anim** on, the timeline follows the Animation window; playing the animation plays the audio in sync.

Toolbar:

- **Re-analyse** – discard the cached analysis and read the clip again.
- **Follow Anim** – line up with the Animation window; off gives its own zoom/scroll row.
- **Bands / Drops / Beats** – frequency lanes, detected bass drops, estimated beat grid.
- **Scrub Audio** – play audio while dragging the animation playhead.
- **|< Drop / Drop >|** – jump between detected drops.
- **Settings** – lanes, lane height, colours, click-to-scrub, log drop times.
- **v1.0.0** – version; click to check for updates.
- **NS** – NekoSuneVR links.

Analysis results are cached in `Library/NekoBeatKeyframing`, so reopening a clip is instant.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `beat-keyframing` branch. If a newer one is out, the version chip in the toolbar turns amber (`v1.0.0 → v1.1.0`) and a slim row under the toolbar offers **Download update**, **Later** and **Skip this version**. Click the chip to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `beat-keyframing` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

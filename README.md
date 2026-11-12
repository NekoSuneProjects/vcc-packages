# NekoSune World UI Assistant

Build VRChat world UI without knowing Unity UI, and learn how it works while you do. Everything the Assistant makes is ordinary Unity UI you can open and edit by hand.

> **Preview (0.1.0).** This is Phase 1 of [TODO.md](TODO.md): building panels. Button actions, lessons, the UI Doctor and ready-made systems come next.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune World UI Assistant** (`com.nekosune.world-ui-assistant`) to a **world** project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

Needs the VRChat Worlds SDK and TextMeshPro. If TextMeshPro's essentials aren't imported, the Assistant offers a button for it.

## Use it

Open **NekoSune → World → UI Assistant**.

1. **New panel** – pick a name, size, theme and where to put it, then **Create Panel**. It appears in front of the Scene view camera, ready for the VRChat laser.
2. **Add** – click Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Row, Grid or Scroll list. New items go after the selected item, or inside a selected Row, Grid or Scroll list.
3. **Outline** – drag items to reorder them, or drop them on a Row, Grid or Scroll list to put them inside. Show/hide, duplicate and delete from each row.
4. **Edit** – change text, colours, sizes and more in plain English. Each item tells you which Unity components it's made of.
5. **Theme** – click a theme on the panel card to re-colour the whole panel.

Every change is one Undo step. Selecting something in the Hierarchy or Scene view selects it in the Assistant too.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `world-ui-assistant` branch. If a newer one is out, the version chip in the header turns amber and a banner offers **Download update**, **Later** and **Skip this version**.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `world-ui-assistant` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

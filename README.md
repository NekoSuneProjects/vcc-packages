# NekoSune World UI Assistant

Build VRChat world UI without knowing Unity UI, and learn how it works while you do. Everything the Assistant makes is ordinary Unity UI you can open and edit by hand.

> **Preview (0.5.0).** Everything in [TODO.md](TODO.md) is built. Still to do: testing in the VRChat client (desktop and VR), recording the lesson videos, and translating lesson/glossary text.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune World UI Assistant** (`com.nekosune.world-ui-assistant`) to a **world** project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

Needs the VRChat Worlds SDK 3.10.1 or newer and TextMeshPro. Works in Unity 2022.3 (VRChat's version) and compiles in Unity 6. If TextMeshPro's essentials aren't imported, the Assistant offers a button for it.

## Use it

Open **NekoSune → World → UI Assistant**.

1. **New panel** – pick a name, size, theme and where to put it, then **Create Panel**. It appears in front of the Scene view camera, ready for the VRChat laser.
2. **Add** – click Title, Text, Button, Toggle, Slider, Image, Divider, Spacer, Row, Grid, Scroll list or Page. New items go after the selected item, or inside a selected Row, Grid or Scroll list.
3. **Outline** – drag items to reorder them, or drop them on a Row, Grid or Scroll list to put them inside. Show/hide, duplicate and delete from each row.
4. **Edit** – change text, colours, sizes and more in plain English. Each item tells you which Unity components it's made of.
5. **Actions** – select a button or toggle and pick what it does under **When it's clicked**: show/hide something, turn a mirror on/off, play or stop a sound, teleport the player, open a page, or send an event to your own Udon script. Tick **Everyone sees it (synced)** or keep it local.
6. **Pages** – click **Page** to add a page with a Back button (the first time, your panel becomes the Home page). Point a button at it with **Open a page**.
7. **Theme** – click a theme on the panel card to re-colour the whole panel.

Before you upload, the panel card checks that every action has what it needs and is connected.

### Templates

| Category | Templates |
|---|---|
| Boards | Welcome board, Rules, Credits, Event schedule, Links / QR |
| Menus | Settings menu, Teleport menu, Tabbed menu, Portable menu, Hotkeys, Admin panel |
| Media | Video player controls, Music player, Remote news board, Gallery / slideshow |
| Shop | Store / market (Creator Economy + external shops with QR codes) |
| Games | Keypad / code door, Scoreboard, Game HUD, Achievements |
| Social | Poll, Dialogue box, Player list & instance info, Avatar board |
| Pieces | Map – you are here, Photo spot, Clock & countdown, Message popup, Welcome with "Don't show again" |

Each system is normal UI plus one short UdonSharp script in `Runtime/`. Its settings appear in the **System settings** card of the Build tab, and each has a lesson.

### How actions work

Each button or toggle with an action gets the `NekoUiAction` UdonSharp script (`Runtime/NekoUiAction.cs`, short and commented). Its On Click calls `UdonBehaviour.SendCustomEvent("Run")`, because VRChat UI can only call Udon that way. Synced actions use `[UdonSynced]` state, so players who join later see the same thing.

### The tabs

- **Build** – make and edit panels (everything above).
- **Learn** – lessons that use your own panel, with progress and badges. Start with *1. What a Canvas is*.
- **Doctor** – checks all panels for common problems and fixes them in one click.
- **Templates** – 28 ready-made panels and systems (see below), the QR code maker and JSON blueprints.
- **Glossary** – every term in plain English.

The toolbar under the tabs sets the **Help** level (Full help, Some help, Expert), **Explain mode** (shows the Unity component behind each setting) and the **language** (12 languages).

Every change is one Undo step. Selecting something in the Hierarchy or Scene view selects it in the Assistant too.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `world-ui-assistant` branch. If a newer one is out, the version chip in the header turns amber and a banner offers **Download update**, **Later** and **Skip this version**.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `world-ui-assistant` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

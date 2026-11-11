# NekoSune World Avatar Search

A beginner-friendly VRChat world avatar browser/search builder. Standalone: no NekoSune World Hub needed.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune World Avatar Search** (`com.nekosune.world-avatar-search`) to your world project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

Open it from **NekoSune → World → Avatar Search Builder**.

## Demo endpoint

The included demo preset uses:

```text
https://vrcavatarsearch.nekosunevr.co.uk/vrcx_search?search=Rindo
```

The generated `VRCUrlInputField` is prefilled with that complete URL. In VRChat a user can edit the search value inside the URL and press **SEARCH**, or press **DEMO RINDO** to use the creator-predeclared preset directly.

The default mapper expects the VRCX-style root-array fields `id`, `name`, `authorName`, `description`, `thumbnailImageUrl`, and `releaseStatus`.

## Flexible JSON adapter

The runtime can also read APIs wrapped in `avatars`, `results`, `items`, or `data`, and tries common aliases such as:

- `id`, `avatarId`, `avatar_id`
- `name`, `avatarName`, `title`
- `authorName`, `author`, `creatorName`
- `description`, `desc`, `summary`
- `releaseStatus`, `status`, `visibility`
- `thumbnailImageUrl`, `thumbnailUrl`, `imageUrl`

All preferred keys remain editable in the generated behaviour.

## Paging

The browser keeps up to 128 mapped results by default. The creator chooses **5** or **10 results per page** when building the UI, and players can switch between 5/page and 10/page from the generated footer. Ten reusable result slots are generated once and recycled across every page.

```text
[ ‹ PAGE ]   Page 2 / 7 • 68 results   [ PAGE › ]
              [ 5 / PAGE ] [ 10 / PAGE ]
```

## UdonSharp program-asset repair

Older versions attached the generated `NekoAvatarSearchBrowser` with Unity's normal `AddComponent` flow. That can leave a C# proxy with no valid `UdonSharpProgramAsset`, producing errors such as:

```text
Unable to find valid U# program asset associated with script 'NekoAvatarSearchBrowser'
```

The Builder now uses **AUTO-WIRE / REPAIR SELECTED SEARCH UI**. It:

1. waits until Unity has finished compiling/importing;
2. verifies `NekoAvatarSearchBrowser.cs` exists and has compiled;
3. finds or creates `Assets/NekoSune/AvatarSearch/Generated/NekoAvatarSearchBrowser.asset`;
4. associates that program asset with the generated MonoScript;
5. requests an UdonSharp compile when repair is needed;
6. stops before attaching anything if UdonSharp still needs to compile;
7. attaches the behaviour through UdonSharp's editor `AddUdonSharpComponent` API;
8. wires the fields/buttons and copies proxy data to the backing UdonBehaviour.

If the repair creates the program asset, wait for Unity/UdonSharp to finish and click Auto-Wire once more.

## Search input limitation

VRChat Udon cannot construct an arbitrary `VRCUrl` from a normal runtime string. The package therefore supports creator-predeclared preset/demo searches plus a real `VRCUrlInputField` for a complete user-entered API URL.

If an API domain is not trusted by VRChat, the player must enable **Allow Untrusted URLs**.

`thumbnailImageUrl` is parsed and retained as metadata, but a thumbnail URL arriving only as a JSON string cannot be turned into a new `VRCUrl` by Udon. The demo therefore uses the real 3D `VRCAvatarPedestal` as its selected-avatar preview instead of pretending arbitrary JSON thumbnail URLs can always be downloaded. A creator can still add predeclared thumbnail `VRCUrl` mappings if their catalog is known at upload time.

## Avatar switching

Results store the avatar ID and use a `VRCAvatarPedestal`:

1. **PREVIEW** calls `SwitchAvatar(id)` on the preview pedestal.
2. **USE AVATAR** switches the pedestal then calls `SetAvatarUse(Networking.LocalPlayer)`.

VRChat continues to enforce the normal public/private/Marketplace ownership rules.

## Demo / repair workflow

1. Open `NekoSune > World > Avatar Search Builder`.
2. Choose 5 or 10 results per page.
3. Click **BUILD AVATAR SEARCH DEMO** for a fresh UI, or select an existing generated UI.
4. Let Unity finish compiling.
5. Select `Neko Avatar Search UI`.
6. Click **AUTO-WIRE / REPAIR SELECTED SEARCH UI**.
7. If told a program asset was created/repaired, wait for UdonSharp and click Auto-Wire again.
8. Test with VRChat Build & Test.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `world-avatar-search` branch. If a newer one is out, the version chip next to the header turns amber (`v1.1.0 → v1.2.0`), a banner offers **Download update**, **Later** and **Skip this version**, and one warning is written to the Console. Click the chip to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `world-avatar-search` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

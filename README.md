# NekoSune VRChat Multi-Account Publisher (v2)

Upload one avatar to all your VRChat accounts with one click. Open **Tools > NekoSune > VRChat Multi-Account Publisher**.

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune VRChat Multi-Account Publisher** (`com.nekosune.multi-publisher`) to your avatar project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you previously had the tool in `Assets/NekoSuneMultiPublisher`, installing the package removes that old copy. Your saved accounts and avatar IDs are stored outside it (see *Data files*) and are kept.

## How it works

You sign in to each account once inside the tool. After that, **Build & Upload to N accounts** goes through them one by one:

1. Switches the VRChat SDK to that account's saved login.
2. Asks VRChat who that login belongs to, and skips the account if it isn't the expected one.
3. Puts that account's Blueprint ID on the avatar's Pipeline Manager (empty = create a new avatar on that account).
4. Builds and uploads through the **official VRChat SDK builder** (`IVRCSdkAvatarBuilderApi.BuildAndUpload`), so VRChat's validation, copyright agreement, permission and ownership checks still run for every account.
5. Saves the Blueprint ID VRChat assigned for that account.

At the end it puts back the SDK login you had before and the avatar's original Blueprint ID. Each account keeps its own avatar ID, so updates go to the right avatar on every account.

The VRChat SDK must be installed (Creator Companion project). The tool opens the SDK control panel itself when needed.

## Setup

1. **Accounts:** click **+ Sign in to an account**, enter the username/email and password, then the 2FA code if asked. Repeat for each account. If the SDK panel is already logged in, **Add SDK's current login** saves that account without typing anything.
2. Give each account a nickname (Main, Alt 1, ...) and tick the ones that should receive uploads.
3. **Avatar:** select the avatar root in the scene and fill in the same info as the SDK panel: **Name**, **Visibility**, **Primary / Secondary Style**, **Content Warnings**, **Tags** (comma separated) and **Description**. Pick a thumbnail with **Select Image** (any PNG/JPG) or **Capture In Scene** (4:3 shot of the Scene view). The thumbnail is required the first time the avatar is created on an account.
4. If an account already has this avatar, paste its `avtr_...` ID in that account's row; otherwise leave it empty and a new avatar is created.
5. Tick the **Platform(s)**: Windows (PC), Android (Quest), iOS.
6. Click **Build & Publish**. Watch progress per account and platform; **Cancel** stops after the current step.

By default, re-uploads keep the info and thumbnail that are already on VRChat. Tick the two "Also apply..." options to push your changes to existing uploads too.

### Platforms

Uploads run platform by platform (the current one first), uploading to every account on each platform, so Unity switches platform as few times as possible. Switching platform makes Unity reload scripts (the first switch to a platform can take minutes while assets reimport); the upload continues by itself after each reload and switches back to the platform you started on at the end. Keep the same scene open and don't close Unity while it runs.

Each platform needs its Unity build support module (Unity Hub > Installs > 2022.3.22f1 > Add modules > Android / iOS Build Support). Android and iOS uploads must pass VRChat's mobile validation (mobile shaders, limits); the SDK reports what to fix.

## Logins and security

- Your **password is never saved**. It is only kept in memory until sign-in finishes.
- What is saved is the VRChat login session (the same token the SDK keeps for you), **encrypted with Windows DPAPI** so only your Windows user on this PC can read it. The file is `%LOCALAPPDATA%\NekoSuneVR\MultiPublisher\accounts.json`, outside the Unity project, so it is never committed, shared or packed into a unitypackage.
- **Remove** forgets an account and its session on this PC. Signing out of VRChat everywhere (website > change password / log out all sessions) invalidates saved sessions too; the tool then shows **Sign in again**.
- Sessions eventually expire. Click **Check** to test one, **Re-login** to renew it.
- On macOS/Linux sessions are not saved (no DPAPI).

## Data files

- Accounts: `%LOCALAPPDATA%\NekoSuneVR\MultiPublisher\accounts.json` (encrypted sessions).
- Avatar IDs per account: `<project>/UserSettings/NekoSuneMultiPublisher/avatars.json`. Back this up; it holds which avatar ID belongs to which account.
- IDs from the old two-slot version (`UserSettings/NekoSuneMultiPublisher-<hash>.json`) are picked up automatically when the account's display name or username matches the old "expected username".

## Notes and limits

- Builds once per account and platform (the SDK's upload API builds and uploads together), so 2 accounts × 3 platforms = 6 builds.
- Styles are loaded from VRChat and need an SDK login; until then the menus show "Not Specified" (click Retry).
- Every account must be allowed to upload avatars, and you must have the rights to upload the avatar on each one. VRChat's Terms of Service apply to every account.
- Don't use the SDK control panel or recompile scripts while an upload is running.
- Requires Unity 2022.3 and VRChat Avatars SDK 3.10.x (built against 3.10.5). Test it with a private avatar first.

## Updates

The tool checks for a newer version when Unity loads (at most every 6 hours) by reading `version.json` from the `multi-publisher` branch. If a newer one is out, the version chip in the header turns amber (`v2.1.0 → v2.2.0`), a banner offers **Download update**, **Later** and **Skip this version**, and one warning is written to the Console. Click the chip to check right away.

## Releasing an update

Bump the same version in all three places, add a `CHANGELOG.md` entry and push to the `multi-publisher` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing.

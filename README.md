# Motion Converter — Fortnite + MMD for VRChat

One Unity Editor window, one tab per animation source. Open **Tools → Motion Converter** and pick a tab (**Generic FBX / .anim**, **Fortnite (.ueanim)**, **MMD / VMD**, and any future module).

## Install

Add the NekoSune VCC repository to the VRChat Creator Companion, then add **NekoSune Motion Converter** (`com.nekosune.motion-converter`) to your project:

```text
https://nekosuneprojects.github.io/vcc-packages/index.json
```

If you previously copied the tool into `Assets/MotionConverter`, the package removes that old copy on install.

## Languages

The dropdown in the top-right of the window switches the whole UI, log messages and errors included: **English, Español, Русский, Polski, Deutsch, Français, Português (Brasil), 日本語, 한국어, 简体中文**. The choice is remembered per machine; on first open it follows your system language (English if it isn't one of these).

- Code is written in English and wrapped in `L.T("...")` (or `L.C(label, tooltip)` for GUIContent). `L.T("Saved: {0}", path)` fills in placeholders.
- Each language is one file in `Editor/Localization/Languages/Lang.<code>.cs`, a dictionary from the English text to the translation. Edit a value there to fix a translation.
- Anything missing from a language falls back to English, so a new string never breaks the UI; it just shows in English until translated.
- To add a language: copy `Lang.es.cs`, rename the dictionary field, translate the values and add one line to `L.Languages` in `Editor/Localization/L.cs`.

The English disclaimers are the original wording; the translations are provided for convenience.

## Updates

Like lilToon, the tool checks for a newer version: when Unity loads (at most every 6 hours) it downloads `version.json` from `UpdateChecker.VersionUrl` and compares it with `UpdateChecker.CurrentVersion`. If a newer one is out:

- the version chip in the header turns orange (`v2.0.0 → v2.1.0`),
- a banner at the top offers **Download update**, **Later** (hide until Unity restarts) and **Skip this version**,
- one warning is written to the Console.

Click the version chip to check right away. Offline or unreachable: nothing is shown, the tool works as normal.

To release an update, bump the same version in all three places and push to the `motion-converter` branch:

- `version` in `package.json`
- `CurrentVersion` in `Editor/Update/UpdateChecker.cs`
- `version` in `version.json` (optional `url` / `notes`)

The release workflow refuses to publish if the three don't match. It then builds the VPM ZIP, creates the GitHub release and rebuilds the VCC listing. `VersionUrl` reads `version.json` straight from the branch, so the in-editor banner updates as soon as the push lands.

## Output naming (all modules)

Every module saves the same way. Fill in **Artist** and **Title** and the clip goes to:

```
Assets/[Motions Converted]/<Artist>/<Title>/<Artist> - <Title>.anim
Assets/[Motions Converted]/<Artist>/<Title>/<Artist> - <Title> (Camera).anim   (MMD camera, if converted)
```

With neither set, the clip is saved next to its source file (`<Source>_Humanoid.anim`, `<CameraVmd>_Camera.anim`). Converting again overwrites the clip in place, so controllers and menus that reference it keep working.

## Generic FBX / .anim

Drop any Generic (Transform-curve) AnimationClip, such as the `.anim` that comes with an exported FBX, into **Source clip**, set Artist / Title, timing and output options, then **Convert to Humanoid**.

## Fortnite (.ueanim)

> **Disclaimer:** I am not responsible for your actions in ripping assets or emotes from Fortnite, whether with FortnitePorting, FModel or any other form of Fortnite asset ripping. This tool only converts motions into working Humanoid avatar animations. It has nothing to do with Fortnite ripping or any other such actions. You are responsible for having the rights to any animation you convert and use. Fortnite is a trademark of Epic Games; this tool is not affiliated with or endorsed by Epic Games.

The Fortnite tab shows this disclaimer at the top, and **Convert** stays disabled until you tick "I have read and understand this disclaimer" (remembered per machine).

Drag the `.ueanim` file in directly, no FBX needed. The CLF / CLM / CMF / CMM files are the same emote for different body types; any one works. (An exported FBX + .anim clip goes in the **Generic FBX / .anim** tab.)

How it works: the file only holds per-bone keys (no hierarchy or mesh), so the importer rebuilds the standard UE skeleton by bone name (pelvis, spine_01-05, neck_01/02, head, clavicles, arms, fingers, legs, balls), converts UE axes (X forward, Y right, Z up, cm) to Unity (permutation, no mirroring) and turns it into a Generic clip that then goes through the same pipeline as the Generic tab (T-pose, humanoid avatar, muscle bake). Twist, IK, face, prop and cloth bones are ignored. Compressed .ueanim files are rejected; export with compression None.

## MMD / VMD

> **Credits & licenses:** Before converting any MMD motion or camera, always check its credits and license (readme / distribution page) and only convert it if the creator allows it. Breaking a license, or making converted motions public without the creator's permission, can get you into serious trouble. Respect other MMD animators' work and credit them the way their terms ask. Do not abuse this tool for anything beyond what the original creators allow.

The MMD tab shows this notice at the top, and **Convert MMD / VMD** stays disabled until you tick "I have read and understand this disclaimer" (remembered per machine).

Import your `.vmd` files into the project's Assets folder. Pick a target Humanoid VRChat avatar, the body motion and optionally one camera VMD (moving or fixed), set Artist / Title, configure options and use **Convert MMD / VMD**.

Only one camera input is shown. Both `camera.vmd` and `camera fixed.vmd` are alternative motions for that single input; you can convert either one by swapping the selected file. Camera output is for a Camera GameObject in a Unity scene or supported world setup: it **cannot override the viewpoint of a VRChat player** or work as a standalone avatar camera feature.

### How the body retarget works

1. **Coordinate conversion.** MMD and Unity are both left-handed and MMD models face -Z, so the conversion is a 180 degree turn about Y: position (x,y,z) -> (-x,y,-z), quaternion (x,y,z,w) -> (-x,y,-z,w).
2. **Absolute world rotations.** Each Humanoid bone gets the full MMD world rotation of its chain (全ての親 > センター > グルーブ > 腰 > 上半身/下半身 > ... including 肩P and 腕捩/手捩 helper bones).
3. **Rest-pose alignment.** The avatar's rest bone directions are rotated onto the source rest directions (arms to the A-pose angle you set, legs/spine to vertical) so a T-pose avatar plays an A-pose-authored VMD correctly.
4. **Foot IK.** 左足ＩＫ/右足ＩＫ target = avatar rest ankle + scaled MMD offset, in the same space and scale as the hips. A two-bone solver places the knee and keeps it a pure hinge so Mecanim doesn't clamp it. Foot orientation follows the IK bone.
5. **Bezier interpolation** from the VMD key data is used for bone, centre and IK tracks.

Settings to know:
- **Source leg length** (default 9.5 MMD units): scale = avatar leg length / this. Bigger = smaller steps and drops.
- **Source arm A-pose angle** (default 40): how far below horizontal the source model's rest arms were. 0 = T-pose source.
- **Start at origin**: removes the first frame's horizontal offset from hips and feet together.

### Limitations

- Retargeting uses the standard MMD bone structure. A source PMX is not read, so source proportions come from two sliders (leg length, arm A-pose angle).
- VMD IK on/off keys, PMX knee/axis limits, physics and append bones other than the standard chain are not reproduced.
- MMD morphs only map to existing similarly named blendshapes.
- Camera interpolations are approximated with linear curves. VMD perspective flags are not animated.
- Humanoid muscles are clamped to the avatar's muscle limits, so poses past a human range get limited.
- Requires Unity 2022.3 Editor. Test the clip on your avatar before uploading to VRChat.

## Code layout

```
Editor/
  Core/                 shared engine, no UI
    ConversionSettings, ConversionLog, OutputLocation (Artist/Title naming + save)
    SourceRig, BoneMap, PoseNormalizer, HumanoidBaker, MotionConversion (Generic clip -> Humanoid)
    ModelRigSetup        (switch an FBX to Humanoid)
  Modules/
    MotionSourceModule   base class for a tab + automatic discovery
    ClipPipelineModule   base for any source that yields a Generic clip (naming, timing, convert, avatar, log)
    Generic/             GenericModule (any Generic FBX / .anim clip)
    Fortnite/            FortniteModule (.ueanim + disclaimer), UeAnimImporter
    MMD/                 MmdModule (UI + license notice), VmdFile, MmdSkeleton, MmdHumanoidBaker, MmdCameraBaker, MmdCurves, MmdSettings
  Localization/
    L                    translation lookup + language picker
    Languages/           Lang.<code>.cs, one translation table per language
  Update/
    UpdateChecker        version check against version.json (lilToon-style)
  Window/
    MotionConverterWindow  header (incl. language picker, version chip), update banner, tab bar, footer
    MotionConverterGUI     shared cards / naming fields / log / result rows
    MotionConverterStyles  colours and styles
    MotionConverterAbout   footer about strip + social links (edit the Links list to add/change one)
```

### Adding a new source

Create a `[Serializable]` class with a parameterless constructor in `Editor/Modules/<Name>/`. The window finds it automatically; nothing else needs registering.

If your format can be turned into a Generic (Transform-curve) `AnimationClip`, derive from `ClipPipelineModule` and you get naming, Artist/Title output, timing, conversion, avatar setup and the log for free:

```csharp
[Serializable]
public sealed class MyFormatModule : ClipPipelineModule
{
    [SerializeField] private DefaultAsset file;

    public override string Id => "myformat";
    public override string TabName => "My Format";
    public override int Order => 30;                 // tab position (Generic 0, Fortnite 10, MMD 20)
    public override string Subtitle => "My Format  →  Humanoid";
    public override string Description => "Convert .myformat motions into Humanoid clips.";

    protected override void DrawSourceInput()
    {
        file = (DefaultAsset)EditorGUILayout.ObjectField("Source file", file, typeof(DefaultAsset), false);
        // settings.sourceClip = <Generic AnimationClip built from the file>;
        DrawClipFacts();
    }
}
```

For anything else, derive from `MotionSourceModule`, draw your own cards with `MotionConverterGUI` and save with `OutputLocation.PathFor(artist, title, fallbackPath)` + `OutputLocation.Save` so it follows the same `[Motions Converted]` layout.

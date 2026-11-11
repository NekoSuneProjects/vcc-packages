// NekoSune MMD -> VRChat VMD importer. Unity 2022.3 Editor only.
// Bakes body/face VMD motion to a Humanoid muscle clip for the selected avatar, and camera VMD to a Unity camera clip.
// No third party dependencies; does not modify avatar prefabs or create Expression menu entries.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using S = MotionConverter.MotionConverterStyles;
using G = MotionConverter.MotionConverterGUI;

namespace MotionConverter.Mmd
{
    [Serializable]
    public sealed class MmdModule : MotionSourceModule
    {
        private const string AcceptedPref = "MotionConverter.Mmd.LicenseNoticeAccepted";

        public static string LicenseNotice => L.T(LicenseNoticeText);

        private const string LicenseNoticeText =
            "Before converting any MMD motion or camera, always check its credits and license (readme / distribution page) " +
            "and only convert it if the creator allows it.\n\n" +
            "Breaking a license, or making converted motions public without the creator's permission, can get you into serious " +
            "trouble. Respect other MMD animators' work and credit them the way their terms ask.\n\n" +
            "Do not abuse this tool for anything beyond what the original creators allow.";

        [SerializeField] private MmdSettings settings = new MmdSettings();
        [SerializeField] private GameObject avatar;
        [SerializeField] private DefaultAsset motionFile, cameraFile;
        [SerializeField] private Transform cameraRig;
        [SerializeField] private List<AnimationClip> results = new List<AnimationClip>();
        [SerializeField] private bool showLog = true;

        public override string Id => "mmd";
        public override string TabName => "MMD / VMD";
        public override int Order => 20;
        public override string Subtitle => L.T("MMD / VMD  →  VRChat avatar + camera clips");
        public override string Description => L.T("Import MMD .vmd body, facial and camera files into Unity .anim clips.");

        private string MotionPath => motionFile != null ? AssetDatabase.GetAssetPath(motionFile) : null;
        private string CameraPath => cameraFile != null ? AssetDatabase.GetAssetPath(cameraFile) : null;

        private string BodyOutput => MotionPath == null ? null
            : OutputLocation.PathFor(settings.artist, settings.title, $"{OutputLocation.FolderOf(MotionPath)}/{motionFile.name}_Humanoid.anim");

        private string CameraOutput => CameraPath == null ? null
            : OutputLocation.PathFor(settings.artist, settings.title, $"{OutputLocation.FolderOf(CameraPath)}/{cameraFile.name}_Camera.anim", " (Camera)");

        // ------------------------------------------------------------------ GUI

        public override void DrawGUI()
        {
            G.Disclaimer(L.T("Credits & licenses"), LicenseNotice, AcceptedPref);
            DrawSourceCard();
            DrawConversionCard();
            DrawCompatibilityCard();
            DrawConvertCard();
            G.LogCard(Log, ref showLog);
        }

        private void DrawSourceCard()
        {
            G.BeginCard("AnimationClip Icon", L.T("Source & naming"));
            EditorGUILayout.HelpBox(L.T("Import MMD body/face and camera VMD motion at 30 FPS. Body motion is baked to Humanoid muscle curves; check the resulting motion on your own avatar."), MessageType.Info);
            avatar = (GameObject)EditorGUILayout.ObjectField(L.T("Target VRChat Avatar"), avatar, typeof(GameObject), true);
            motionFile = (DefaultAsset)EditorGUILayout.ObjectField(L.T("Body / Face VMD"), motionFile, typeof(DefaultAsset), false);
            if (MotionPath != null && !VmdFile.IsVmd(MotionPath))
                EditorGUILayout.HelpBox(L.T("That file is not a .vmd."), MessageType.Warning);
            cameraFile = (DefaultAsset)EditorGUILayout.ObjectField(L.T("Camera Motion VMD (any mode)"), cameraFile, typeof(DefaultAsset), false);
            if (CameraPath != null && !VmdFile.IsVmd(CameraPath))
                EditorGUILayout.HelpBox(L.T("That file is not a .vmd."), MessageType.Warning);
            EditorGUILayout.HelpBox(L.T("One camera input: select any MMD camera VMD, including fixed or moving camera motions. Change the file to convert a different camera mode."), MessageType.None);
            cameraRig = (Transform)EditorGUILayout.ObjectField(L.T("Camera Rig (reference only)"), cameraRig, typeof(Transform), true);

            if (GUILayout.Button(L.T("Use .vmd files selected in Project"), EditorStyles.miniButton))
            {
                foreach (var a in Selection.objects.OfType<DefaultAsset>().Where(a => VmdFile.IsVmd(AssetDatabase.GetAssetPath(a))))
                {
                    if (a.name.ToLowerInvariant().Contains("camera")) cameraFile = a; else motionFile = a;
                }
            }

            EditorGUILayout.Space(6);
            G.NamingFields(ref settings.artist, ref settings.title);
            G.OutputPaths(BodyOutput, CameraOutput);
            G.EndCard();
        }

        private void DrawConversionCard()
        {
            G.BeginCard("d_Settings", L.T("Conversion options"));
            settings.hipsTranslation = EditorGUILayout.Toggle(L.T("Hips translation"), settings.hipsTranslation);
            using (new EditorGUI.DisabledScope(!settings.hipsTranslation))
                settings.startAtOrigin = EditorGUILayout.Toggle(L.T("Start at origin (XZ)"), settings.startAtOrigin);
            settings.includeFingers = EditorGUILayout.Toggle(L.T("Retarget fingers"), settings.includeFingers);
            settings.includeExpressions = EditorGUILayout.Toggle(L.T("Retarget face blendshapes"), settings.includeExpressions);
            settings.cameraUsePerspective = EditorGUILayout.Toggle(L.T("Convert Camera FOV"), settings.cameraUsePerspective);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L.T("Source model calibration"), EditorStyles.boldLabel);
            settings.sourceLegLength = EditorGUILayout.Slider(L.T("Source leg length (MMD units)"), settings.sourceLegLength, 5f, 16f);
            settings.armAPoseAngle = EditorGUILayout.Slider(L.T("Source arm A-pose angle (deg)"), settings.armAPoseAngle, 0f, 60f);
            EditorGUILayout.HelpBox(L.T("Scale: avatar leg length / source leg length. If feet float or sink when standing, nudge the leg length (bigger = smaller steps). The arm angle is how far below horizontal the source PMX's rest arms point (0 = T-pose, ~35-45 = A-pose). If arms look too wide or too tight to the body, adjust it."), MessageType.None);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L.T("MMD feet, knees & ground contact"), EditorStyles.boldLabel);
            settings.solveFootIk = EditorGUILayout.Toggle(L.T("Solve MMD foot IK"), settings.solveFootIk);
            using (new EditorGUI.DisabledScope(!settings.solveFootIk))
            {
                settings.footIkStrength = EditorGUILayout.Slider(L.T("Foot IK weight"), settings.footIkStrength, 0f, 1f);
                settings.maxFootReach = EditorGUILayout.Slider(L.T("IK leg extension limit"), settings.maxFootReach, 0.85f, 0.999f);
                settings.keepFeetAboveFloor = EditorGUILayout.Toggle(L.T("Keep feet above start floor"), settings.keepFeetAboveFloor);
            }
            G.EndCard();
        }

        private static void DrawCompatibilityCard()
        {
            G.BeginCard("Avatar Icon", L.T("Avatar compatibility"));
            EditorGUILayout.HelpBox(L.T("Avatar must be Humanoid with mapped bones. Body motion is baked as Humanoid muscle curves for the selected avatar; check feet, hips, shoulders and fingers in Unity before upload. Camera clips are for Unity scene/world cameras, NOT a player's VRChat viewpoint."), MessageType.Warning);
            G.EndCard();
        }

        private void DrawConvertCard()
        {
            G.BeginCard(null, null);
            bool hasInput = motionFile != null || cameraFile != null;
            bool needsAvatar = motionFile != null && avatar == null;
            bool accepted = G.IsAccepted(AcceptedPref);
            if (G.BigButton(L.T("Convert MMD / VMD"), hasInput && !needsAvatar && accepted)) Convert();

            if (!accepted) EditorGUILayout.LabelField(L.T("Read and accept the credits & licenses notice at the top to convert."), S.PathLabel);
            else if (!hasInput) EditorGUILayout.LabelField(L.T("Pick a body/face VMD and/or a camera VMD to start."), S.PathLabel);
            else if (needsAvatar) EditorGUILayout.LabelField(L.T("Pick the target Humanoid avatar to bake the body motion for."), S.PathLabel);

            results.RemoveAll(r => r == null);
            if (results.Count > 0)
            {
                EditorGUILayout.Space(6);
                bool failed = Log.Entries.Any(e => e.level == LogLevel.Error);
                foreach (var r in results) G.ResultRow(r, failed);
            }
            G.EndCard();
        }

        // ------------------------------------------------------------------ actions

        private void Convert()
        {
            Log.Clear();
            results.Clear();
            showLog = true;
            try
            {
                if (motionFile != null)
                {
                    var clip = MmdHumanoidBaker.Bake(avatar, VmdFile.Read(MotionPath), settings, Log);
                    string path = BodyOutput;
                    results.Add(OutputLocation.Save(clip, path));
                    Log.Info(L.T("Saved: {0}", path));
                }

                if (cameraFile != null)
                {
                    var clip = MmdCameraBaker.Bake(VmdFile.Read(CameraPath), avatar, settings, Log);
                    if (clip != null)
                    {
                        string path = CameraOutput;
                        results.Add(OutputLocation.Save(clip, path));
                        Log.Info(L.T("Saved: {0}", path));
                    }
                }

                AssetDatabase.Refresh();
                if (results.Count > 0) EditorGUIUtility.PingObject(results[0]);
            }
            catch (OperationCanceledException e)
            {
                Log.Warn(e.Message);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            GUIUtility.ExitGUI();
        }
    }
}

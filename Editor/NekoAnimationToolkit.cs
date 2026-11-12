using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using S = NekoUnityEssentials.EssentialsStyles;
using G = NekoUnityEssentials.EssentialsGUI;
using Object = UnityEngine.Object;

namespace NekoUnityEssentials
{
    /// <summary>Animation Toolkit tab: new clips, simple start → end transform keys, duplicate with path repair.</summary>
    [Serializable]
    internal class NekoAnimationToolkit
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform target;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string clipName = "New Neko Animation";
        [SerializeField] private float duration = 1f;
        [SerializeField] private Vector3 endPosition = Vector3.zero;
        [SerializeField] private Vector3 endEuler = Vector3.zero;
        [SerializeField] private Vector3 endScale = Vector3.one;
        [SerializeField] private bool animatePosition = true, animateRotation, animateScale;
        [SerializeField] private bool replaceExisting;
        [SerializeField] private string oldPath = "Body";
        [SerializeField] private string newPath = "Armature/Body";
        private string report;
        private bool reportOk;

        public void OnGUI(EditorWindow host)
        {
            DrawTargetCard();
            DrawKeyframeCard();
            DrawRepairCard();
            if (!string.IsNullOrEmpty(report))
            {
                G.BeginCard(null, null);
                G.Report(report, reportOk);
                G.EndCard();
            }
        }

        private void DrawTargetCard()
        {
            G.BeginCard("AnimationClip Icon", "Clip & target");
            root = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Animation root", "The object with the Animator (usually the avatar). Paths are written relative to it."), root, typeof(GameObject), true);
            target = (Transform)EditorGUILayout.ObjectField(new GUIContent("Target bone / object", "Must be the root or one of its children."), target, typeof(Transform), true);
            clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Animation clip", "An editable .anim asset."), clip, typeof(AnimationClip), false);
            duration = Mathf.Max(.01f, EditorGUILayout.FloatField(new GUIContent("Duration (seconds)"), duration));

            G.Group("New clip");
            using (new EditorGUILayout.HorizontalScope())
            {
                clipName = EditorGUILayout.TextField(new GUIContent("Name"), clipName);
                if (GUILayout.Button(S.Icon("Toolbar Plus", " Create"), GUILayout.Width(80), GUILayout.Height(18))) CreateClip();
            }
            G.EndCard();
        }

        private void DrawKeyframeCard()
        {
            G.BeginCard("Animation.Record", "Easy transform keyframes",
                "Keys the target's current local transform at 0s and your end values at the duration.");

            DrawAxisRow(ref animatePosition, "Position", ref endPosition, "End local position");
            DrawAxisRow(ref animateRotation, "Rotation", ref endEuler, "End local rotation (Euler)");
            DrawAxisRow(ref animateScale, "Scale", ref endScale, "End local scale");

            EditorGUILayout.Space(4);
            replaceExisting = EditorGUILayout.ToggleLeft(new GUIContent(" Replace matching curves", "Off: curves that already exist in the clip are left alone."), replaceExisting);
            EditorGUILayout.Space(6);
            bool ready = root && target && clip && (animatePosition || animateRotation || animateScale);
            if (G.Action("Animation.Record", "Write Keyframes (start → end)", ready, true)) WriteKeyframes();
            if (!ready) G.Hint("Pick a root, a target and a clip, and tick at least one of Position / Rotation / Scale.");
            EditorGUILayout.Space(2);
            G.Hint("Rotation uses quaternion keys along the shortest path. Back up a clip before replacing curves.");
            G.EndCard();
        }

        private static void DrawAxisRow(ref bool on, string label, ref Vector3 value, string valueLabel)
        {
            on = EditorGUILayout.ToggleLeft(" " + label, on, EditorStyles.boldLabel);
            if (!on) return;
            EditorGUI.indentLevel++;
            value = EditorGUILayout.Vector3Field(valueLabel, value);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        private void DrawRepairCard()
        {
            G.BeginCard("d_Linked", "Animation path repair",
                "Makes a copy of the clip with renamed binding paths, e.g. after moving or renaming a mesh. The original stays unchanged.");
            oldPath = EditorGUILayout.TextField(new GUIContent("Old path prefix", "Exact path, or a parent path: \"Body\" also matches \"Body/Child\"."), oldPath);
            newPath = EditorGUILayout.TextField(new GUIContent("New path prefix"), newPath);
            EditorGUILayout.Space(6);
            bool ready = clip && !string.IsNullOrEmpty(oldPath) && oldPath != newPath;
            if (G.Action("d_TreeEditor.Duplicate", "Duplicate Clip + Repair Paths", ready)) RepairPaths();
            G.EndCard();
        }

        private void Report(string message, bool ok)
        {
            report = message;
            reportOk = ok;
        }

        private void CreateClip()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save animation clip", clipName, "anim", "Select a project folder");
            if (string.IsNullOrEmpty(path)) return;
            var created = new AnimationClip { frameRate = 60 };
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            clip = created;
            Selection.activeObject = created;
            Report("Created " + path, true);
        }

        private bool ValidateTargets()
        {
            if (!root || !target || !clip)
            {
                EditorUtility.DisplayDialog("Missing references", "Choose a root GameObject, target Transform and animation clip.", "OK");
                return false;
            }
            if (target != root.transform && !target.IsChildOf(root.transform))
            {
                EditorUtility.DisplayDialog("Invalid target", "Target must be the root or one of its children.", "OK");
                return false;
            }
            if (!AssetDatabase.Contains(clip) || (clip.hideFlags & HideFlags.NotEditable) != 0)
            {
                EditorUtility.DisplayDialog("Clip not editable", "Choose an editable .anim asset, not an imported FBX clip.", "OK");
                return false;
            }
            return true;
        }

        private void WriteKeyframes()
        {
            if (!ValidateTargets()) return;
            var path = AnimationUtility.CalculateTransformPath(target, root.transform);
            Undo.RegisterCompleteObjectUndo(clip, "Write Neko animation curves");
            if (animatePosition)
                WriteVector(path, "m_LocalPosition", target.localPosition, endPosition);
            if (animateScale)
                WriteVector(path, "m_LocalScale", target.localScale, endScale);
            if (animateRotation)
            {
                var start = target.localRotation;
                var end = Quaternion.Euler(endEuler);
                if (Quaternion.Dot(start, end) < 0) end = new Quaternion(-end.x, -end.y, -end.z, -end.w);
                WriteVector4(path, "m_LocalRotation", new Vector4(start.x, start.y, start.z, start.w), new Vector4(end.x, end.y, end.z, end.w));
            }
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(clip);
            Report("Wrote keyframes for \"" + (string.IsNullOrEmpty(path) ? root.name : path) + "\" into " + clip.name + ".", true);
        }

        private void WriteVector(string path, string property, Vector3 a, Vector3 b)
        {
            WriteCurve(path, property + ".x", a.x, b.x);
            WriteCurve(path, property + ".y", a.y, b.y);
            WriteCurve(path, property + ".z", a.z, b.z);
        }

        private void WriteVector4(string path, string property, Vector4 a, Vector4 b)
        {
            WriteCurve(path, property + ".x", a.x, b.x);
            WriteCurve(path, property + ".y", a.y, b.y);
            WriteCurve(path, property + ".z", a.z, b.z);
            WriteCurve(path, property + ".w", a.w, b.w);
        }

        private void WriteCurve(string path, string property, float start, float end)
        {
            var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
            if (!replaceExisting && AnimationUtility.GetEditorCurve(clip, binding) != null) return;
            var curve = AnimationCurve.Linear(0, start, duration, end);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static bool IsPrefix(string path, string prefix)
        {
            return path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal);
        }

        private void RepairPaths()
        {
            if (!clip || string.IsNullOrEmpty(oldPath) || oldPath == newPath)
            {
                EditorUtility.DisplayDialog("Invalid input", "Choose an animation and different non-empty old/new paths.", "OK");
                return;
            }
            if (!AssetDatabase.Contains(clip)) return;
            string sourcePath = AssetDatabase.GetAssetPath(clip);
            string destination = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(sourcePath, null) + "_Repaired.anim");
            var copy = Object.Instantiate(clip);
            copy.name = Path.GetFileNameWithoutExtension(destination);
            AssetDatabase.CreateAsset(copy, destination);
            int changed = 0;
            // Read bindings from source and rewrite only the duplicate.
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!IsPrefix(binding.path, oldPath)) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(copy, binding, null);
                var updated = binding;
                updated.path = newPath + binding.path.Substring(oldPath.Length);
                AnimationUtility.SetEditorCurve(copy, updated, curve);
                changed++;
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!IsPrefix(binding.path, oldPath)) continue;
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurve(copy, binding, null);
                var updated = binding;
                updated.path = newPath + binding.path.Substring(oldPath.Length);
                AnimationUtility.SetObjectReferenceCurve(copy, updated, curve);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
            Report("Created " + destination + " and repaired " + changed + " binding" + (changed == 1 ? "" : "s") + ".", changed > 0);
        }
    }
}

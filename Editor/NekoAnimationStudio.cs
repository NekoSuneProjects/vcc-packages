using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using S = NekoUnityEssentials.EssentialsStyles;
using G = NekoUnityEssentials.EssentialsGUI;
using Object = UnityEngine.Object;

namespace NekoUnityEssentials
{
    /// <summary>Animation Studio tab: non-destructive helpers for ordinary .anim assets. Never writes to imported FBX clips.</summary>
    [Serializable]
    internal class NekoAnimationStudio
    {
        [SerializeField] private AnimationClip source;
        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private GameObject selectedObject;
        [SerializeField] private SkinnedMeshRenderer mesh;
        [SerializeField] private string blendShapeName = "";
        [SerializeField] private float blendShapeStart, blendShapeEnd = 100f;
        [SerializeField] private float seconds = 1f;
        [SerializeField] private int framesPerSecond = 60;
        [SerializeField] private float tolerance = 0.001f;
        [SerializeField] private bool startEnabled, endEnabled = true;
        [SerializeField] private bool pingPong;
        [SerializeField] private string leftToken = "Left", rightToken = "Right";
        private string report = "";
        private bool reportError;
        [NonSerialized] private EditorWindow host;

        public void OnEnable()
        {
            if (Selection.activeObject is AnimationClip current) source = current;
        }

        public void OnGUI(EditorWindow window)
        {
            host = window;
            DrawSourceCard();
            DrawClipCard();
            DrawPresetCard();
            DrawBoneCard();
            DrawControllerCard();
            DrawResultCard();
        }

        private void Repaint()
        {
            if (host) host.Repaint();
        }

        /// <summary>Runs a button action with a fresh result line.</summary>
        private void Run(Action action)
        {
            reportError = false;
            action();
        }

        private void DrawSourceCard()
        {
            G.BeginCard("AnimationClip Icon", "Source", "Processing tools write a separate .anim copy next to the source; originals stay unchanged.");
            source = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Source animation", "A standalone .anim asset. Clips inside FBX files are read-only."), source, typeof(AnimationClip), false);
            if (source && !IsEditableAsset(source))
                G.Report("This clip is read-only (inside an FBX or not a .anim). Duplicate it in the Project window first.", false);
            avatarRoot = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Animation root", "The object with the Animator, usually the avatar root."), avatarRoot, typeof(GameObject), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                seconds = Mathf.Max(0.02f, EditorGUILayout.FloatField(new GUIContent("Duration (s)", "Used by the presets below."), seconds));
            }
            framesPerSecond = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Frames per second"), framesPerSecond), 1, 240);
            G.EndCard();
        }

        private void DrawClipCard()
        {
            bool editable = IsEditableAsset(source);
            G.BeginCard("d_TreeEditor.Duplicate", "Clip utilities");
            using (new EditorGUI.DisabledScope(!editable))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent(" Duplicate", EditorGUIUtility.IconContent("d_TreeEditor.Duplicate").image, "Plain copy of the clip."), GUILayout.Height(24)))
                        Run(() => ProcessCopy("Copy", delegate { }));
                    if (GUILayout.Button(new GUIContent(" Reverse", EditorGUIUtility.IconContent("Animation.PrevKey").image, "Play the clip backwards."), GUILayout.Height(24)))
                        Run(() => ProcessCopy("Reversed", Reverse));
                }

                G.Group("Loop");
                pingPong = EditorGUILayout.ToggleLeft(new GUIContent(" Ping-pong (forward then reverse)", "Doubles the length: plays forward, then back."), pingPong);
                if (GUILayout.Button(new GUIContent(" Generate Loop Clip", EditorGUIUtility.IconContent("preAudioLoopOff").image), GUILayout.Height(24)))
                    Run(() => ProcessCopy(pingPong ? "PingPong" : "Loop", c => MakeLoop(c, pingPong)));

                G.Group("Simplify");
                using (new EditorGUILayout.HorizontalScope())
                {
                    tolerance = Mathf.Max(0.000001f, EditorGUILayout.FloatField(new GUIContent("Tolerance", "Interior keys within this distance of a straight line are removed."), tolerance));
                    if (GUILayout.Button(new GUIContent("Reduce Keys", "Remove redundant interior float keys; first and last keys are kept."), GUILayout.Width(100)))
                        Run(() => ProcessCopy("Optimized", c => Simplify(c, tolerance)));
                }
            }
            if (!editable) G.Hint("Pick an editable source clip above to use these.");
            G.EndCard();
        }

        private void DrawPresetCard()
        {
            G.BeginCard("Animation.Record", "Quick animation presets", "Creates a new clip; the object must be under the animation root.");

            G.Group("On / off toggle", true);
            selectedObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Object to animate", "Clothing, accessory or prop."), selectedObject, typeof(GameObject), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                startEnabled = EditorGUILayout.ToggleLeft("On at start", startEnabled, GUILayout.Width(100));
                endEnabled = EditorGUILayout.ToggleLeft("On at end", endEnabled, GUILayout.Width(100));
            }
            if (G.Action("Toggle Icon", "Create GameObject On/Off Clip", selectedObject && avatarRoot)) Run(CreateToggle);

            EditorGUILayout.Space(6);
            G.Group("Blendshape");
            mesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(new GUIContent("Renderer"), mesh, typeof(SkinnedMeshRenderer), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                blendShapeName = EditorGUILayout.TextField(new GUIContent("Blendshape"), blendShapeName);
                using (new EditorGUI.DisabledScope(!(mesh && mesh.sharedMesh && mesh.sharedMesh.blendShapeCount > 0)))
                    if (GUILayout.Button(new GUIContent("Pick…", "Choose from the renderer's blendshapes."), EditorStyles.popup, GUILayout.Width(64)))
                        ShowBlendshapeMenu();
            }
            blendShapeStart = EditorGUILayout.Slider("From", blendShapeStart, 0f, 100f);
            blendShapeEnd = EditorGUILayout.Slider("To", blendShapeEnd, 0f, 100f);
            if (G.Action("SkinnedMeshRenderer Icon", "Create Blendshape Animation", mesh && avatarRoot && !string.IsNullOrEmpty(blendShapeName))) Run(CreateBlendshape);
            if (!avatarRoot) G.Hint("Set the animation root in Source first.");
            G.EndCard();
        }

        private void ShowBlendshapeMenu()
        {
            var menu = new GenericMenu();
            for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
            {
                string shape = mesh.sharedMesh.GetBlendShapeName(i);
                menu.AddItem(new GUIContent(shape.Replace("/", "∕")), shape == blendShapeName, () => { blendShapeName = shape; Repaint(); });
            }
            menu.ShowAsContext();
        }

        private void DrawBoneCard()
        {
            G.BeginCard("AvatarSelector", "Avatar / bone tools");
            using (new EditorGUILayout.HorizontalScope())
            {
                leftToken = EditorGUILayout.TextField(new GUIContent("Left / Right tokens", "Text that marks each side in bone names, e.g. Left/Right, .L/.R, _L/_R."), leftToken);
                rightToken = EditorGUILayout.TextField(rightToken, GUILayout.Width(80));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!IsEditableAsset(source)))
                    if (GUILayout.Button(new GUIContent(" Swap Left / Right Paths", EditorGUIUtility.IconContent("d_Mirror").image, "Remaps paths only; it does not mirror rotations or poses."), GUILayout.Height(24)))
                        Run(() => ProcessCopy("MirroredPaths", MirrorPaths));
                using (new EditorGUI.DisabledScope(!source || !avatarRoot))
                    if (GUILayout.Button(new GUIContent(" Find Missing Paths", EditorGUIUtility.IconContent("Search Icon").image, "List animated paths that don't exist under the animation root."), GUILayout.Height(24)))
                        Run(InspectPaths);
            }
            G.Hint("Swapping remaps binding paths only; it does not mathematically mirror rotations or poses.");
            G.EndCard();
        }

        private void DrawControllerCard()
        {
            G.BeginCard("AnimatorController Icon", "Animator Controller builder");
            int selected = Selection.objects.OfType<AnimationClip>().Count(IsEditableAsset);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Selected .anim clips in the Project window:", EditorStyles.wordWrappedLabel);
                GUILayout.FlexibleSpace();
                G.Chip(selected.ToString(), selected > 0 ? S.Success : S.Muted);
            }
            EditorGUILayout.Space(4);
            if (G.Action("AnimatorController Icon", "Create Controller from Selected Clips", selected > 0)) Run(BuildController);
            G.Hint("One state per clip. Never overwrites an existing controller and doesn't add transitions or parameters.");
            G.EndCard();
        }

        private void DrawResultCard()
        {
            if (string.IsNullOrEmpty(report)) return;
            G.BeginCard(null, null);
            G.Report(report, !reportError);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy", EditorStyles.miniButtonLeft, GUILayout.Width(56))) EditorGUIUtility.systemCopyBuffer = report;
                if (GUILayout.Button("Clear", EditorStyles.miniButtonRight, GUILayout.Width(56))) report = "";
            }
            G.EndCard();
        }

        private static bool IsEditableAsset(AnimationClip clip)
        {
            return clip && AssetDatabase.GetAssetPath(clip).EndsWith(".anim", StringComparison.OrdinalIgnoreCase) &&
                   AssetDatabase.IsMainAsset(clip) && (clip.hideFlags & HideFlags.NotEditable) == 0;
        }

        private void ProcessCopy(string suffix, Action<AnimationClip> modify)
        {
            if (!IsEditableAsset(source)) { Notice("Select a writable standalone .anim clip. Imported FBX clips cannot be edited directly."); return; }
            try
            {
                string srcPath = AssetDatabase.GetAssetPath(source);
                string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(Path.GetDirectoryName(srcPath) ?? "Assets", Path.GetFileNameWithoutExtension(srcPath) + "_" + suffix + ".anim").Replace('\\', '/'));
                var copy = Object.Instantiate(source);
                copy.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(copy, path);
                try { modify(copy); }
                catch { AssetDatabase.DeleteAsset(path); throw; }
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
                Selection.activeObject = copy;
                EditorGUIUtility.PingObject(copy);
                report = "Created: " + path;
            }
            catch (Exception ex) { Debug.LogException(ex); Notice("Could not process clip: " + ex.Message); }
        }

        private static void Reverse(AnimationClip clip)
        {
            float length = clip.length;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var old = curve.keys;
                var reversed = new Keyframe[old.Length];
                for (int i = 0; i < old.Length; ++i)
                {
                    var k = old[old.Length - 1 - i];
                    reversed[i] = new Keyframe(length - k.time, k.value, -k.outTangent, -k.inTangent);
                }
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(reversed));
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                var reverse = frames.Reverse().Select(f => new ObjectReferenceKeyframe { time = length - f.time, value = f.value }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, binding, reverse);
            }
        }

        private static void MakeLoop(AnimationClip clip, bool doubleLength)
        {
            if (doubleLength)
            {
                float length = clip.length;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    var keys = curve.keys.ToList();
                    foreach (var k in curve.keys.Reverse().Skip(1))
                        keys.Add(new Keyframe(2 * length - k.time, k.value, -k.outTangent, -k.inTangent));
                    AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys.ToArray()));
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    var all = keys.Concat(keys.Reverse().Skip(1).Select(k => new ObjectReferenceKeyframe { time = length * 2f - k.time, value = k.value })).ToArray();
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, all);
                }
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        // Remove interior keys only when linear interpolation is close, preserving endpoints.
        private static void Simplify(AnimationClip clip, float threshold)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var original = curve.keys;
                if (original.Length < 3) continue;
                var kept = new List<Keyframe> { original[0] };
                for (int i = 1; i < original.Length - 1; i++)
                {
                    var a = kept[kept.Count - 1];
                    var b = original[i];
                    var c = original[i + 1];
                    if (Mathf.Abs(c.time - a.time) < 0.00001f ||
                        Mathf.Abs(Mathf.Lerp(a.value, c.value, (b.time - a.time) / (c.time - a.time)) - b.value) > threshold)
                        kept.Add(b);
                }
                kept.Add(original[original.Length - 1]);
                if (kept.Count < original.Length) AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(kept.ToArray()));
            }
        }

        private bool CheckObject(Transform target)
        {
            if (avatarRoot && target && (target == avatarRoot.transform || target.IsChildOf(avatarRoot.transform))) return true;
            Notice("Choose an animation root containing the object/renderer to animate.");
            return false;
        }

        private void CreateToggle()
        {
            if (!selectedObject || !CheckObject(selectedObject.transform)) return;
            CreatePreset("ObjectToggle", clip =>
            {
                var binding = EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(selectedObject.transform, avatarRoot.transform), typeof(GameObject), "m_IsActive");
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, seconds, startEnabled ? 1 : 0));
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                curve.AddKey(Mathf.Max(0.01f, seconds - 1f / framesPerSecond), startEnabled ? 1f : 0f);
                curve.AddKey(seconds, endEnabled ? 1f : 0f);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            });
        }

        private void CreateBlendshape()
        {
            if (!mesh || !mesh.sharedMesh || !CheckObject(mesh.transform)) return;
            if (mesh.sharedMesh.GetBlendShapeIndex(blendShapeName) < 0) { Notice("The mesh does not contain a blendshape named '" + blendShapeName + "'."); return; }
            CreatePreset("Blendshape_" + blendShapeName, clip =>
            {
                var path = AnimationUtility.CalculateTransformPath(mesh.transform, avatarRoot.transform);
                var binding = EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + blendShapeName);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0f, blendShapeStart, seconds, blendShapeEnd));
            });
        }

        private void CreatePreset(string name, Action<AnimationClip> setup)
        {
            var path = EditorUtility.SaveFilePanelInProject("Save preset animation", name, "anim", "Choose where to save the animation");
            if (string.IsNullOrEmpty(path)) return;
            var clip = new AnimationClip { frameRate = framesPerSecond };
            AssetDatabase.CreateAsset(clip, path);
            try { setup(clip); }
            catch (Exception ex) { AssetDatabase.DeleteAsset(path); Debug.LogException(ex); Notice(ex.Message); return; }
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            source = clip;
            Selection.activeObject = clip;
            report = "Created: " + path;
        }

        private string Swap(string path)
        {
            if (string.IsNullOrEmpty(leftToken) || string.IsNullOrEmpty(rightToken) || leftToken == rightToken)
                return path;
            const string sentinel = "__NEKO_SWAP_PLACEHOLDER_12345__";
            return path.Replace(leftToken, sentinel).Replace(rightToken, leftToken).Replace(sentinel, rightToken);
        }

        private void MirrorPaths(AnimationClip clip)
        {
            if (string.IsNullOrEmpty(leftToken) || string.IsNullOrEmpty(rightToken) || leftToken == rightToken)
                throw new InvalidOperationException("Enter two different non-empty Left/Right tokens.");
            var floats = AnimationUtility.GetCurveBindings(clip)
                .Select(b => new { binding = b, curve = AnimationUtility.GetEditorCurve(clip, b) }).ToArray();
            var objects = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .Select(b => new { binding = b, keys = AnimationUtility.GetObjectReferenceCurve(clip, b) }).ToArray();
            foreach (var item in floats) AnimationUtility.SetEditorCurve(clip, item.binding, null);
            foreach (var item in objects) AnimationUtility.SetObjectReferenceCurve(clip, item.binding, null);
            foreach (var item in floats) { var b = item.binding; b.path = Swap(b.path); AnimationUtility.SetEditorCurve(clip, b, item.curve); }
            foreach (var item in objects) { var b = item.binding; b.path = Swap(b.path); AnimationUtility.SetObjectReferenceCurve(clip, b, item.keys); }
        }

        private void InspectPaths()
        {
            if (!source || !avatarRoot) { Notice("Choose a source clip and animation root first."); return; }
            var missing = new HashSet<string>();
            var bindings = AnimationUtility.GetCurveBindings(source).Concat(AnimationUtility.GetObjectReferenceCurveBindings(source));
            foreach (var b in bindings)
                if (!string.IsNullOrEmpty(b.path) && !avatarRoot.transform.Find(b.path)) missing.Add(b.path);
            report = missing.Count == 0 ? "No missing transform paths found. (This does not validate every animated component/property.)" :
                missing.Count + " missing paths under '" + avatarRoot.name + "':\n" + string.Join("\n", missing.OrderBy(x => x).ToArray());
        }

        private void BuildController()
        {
            var clips = Selection.objects.OfType<AnimationClip>().Where(IsEditableAsset).Distinct().ToArray();
            if (clips.Length == 0) { Notice("Select one or more standalone .anim files in the Project window first."); return; }
            var path = EditorUtility.SaveFilePanelInProject("Save Animator Controller", "NekoAnimator", "controller", "Save controller to Assets");
            if (string.IsNullOrEmpty(path)) return;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var c in clips)
            {
                var state = controller.layers[0].stateMachine.AddState(c.name);
                state.motion = c;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Selection.activeObject = controller;
            report = "Created " + path + " with " + clips.Length + " states. Configure transitions and VRChat FX layers manually.";
        }

        private void Notice(string message) { report = message; reportError = true; EditorUtility.DisplayDialog("Neko Animation Studio", message, "OK"); }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace NekoSuneVR.VRChatIdleAudio
{
    public enum InstallBackend
    {
        VRCFury,
        ModularAvatar
    }

    /// <summary>
    /// Everything the Intro and AFK Idle installers both need: audio sources, animation clips,
    /// Play Audio behaviours, expression menus, Doppel ScreenFX setup, and merging the finished
    /// controller onto the avatar through VRCFury or Modular Avatar.
    ///
    /// Kept backend-agnostic on purpose - both backends are reached by reflection, so the package
    /// compiles whether you have one, the other, or both installed.
    /// </summary>
    public static class NekoInstallerCore
    {
        public const string DoppelShaderName235 = "Doppels shaders/ScreenFX/Dope Shader 2.3.5";

        public enum DoppelRendererMode
        {
            ParticleSystem,
            SkinnedMeshRenderer
        }

        public enum DoppelPreset
        {
            DigitalBoot,
            AFKLoading
        }

        // ------------------------------------------------------------------
        // Reflection helpers
        // ------------------------------------------------------------------

        public static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        public static bool HasVRCFury => FindType("com.vrcfury.api.FuryComponents") != null;

        public static bool HasModularAvatar =>
            FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator") != null;

        public static bool BackendAvailable(InstallBackend backend)
        {
            return backend == InstallBackend.VRCFury ? HasVRCFury : HasModularAvatar;
        }

        public static void SetEnumMember(object target, string memberName, string enumName)
        {
            if (target == null) return;
            var type = target.GetType();

            var field = type.GetField(memberName);
            if (field != null && field.FieldType.IsEnum)
            {
                field.SetValue(target, Enum.Parse(field.FieldType, enumName));
                return;
            }

            var property = type.GetProperty(memberName);
            if (property != null && property.CanWrite && property.PropertyType.IsEnum)
                property.SetValue(target, Enum.Parse(property.PropertyType, enumName));
        }

        public static void SetMember(object target, string memberName, object value)
        {
            if (!TrySetMember(target, memberName, value))
                throw new MissingMemberException(target.GetType().FullName, memberName);
        }

        public static bool TrySetMember(object target, string memberName, object value)
        {
            if (target == null || value == null) return false;
            var type = target.GetType();

            var field = type.GetField(memberName);
            if (field != null && field.FieldType.IsAssignableFrom(value.GetType()))
            {
                field.SetValue(target, value);
                return true;
            }

            var property = type.GetProperty(memberName);
            if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(value.GetType()))
            {
                property.SetValue(target, value);
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Paths
        // ------------------------------------------------------------------

        public static string SafeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Avatar";
            return string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        }

        public static string OutputFolder(GameObject avatarRoot, string subFolder)
        {
            return "Assets/NekoSuneVR/Generated/IntroIdleAudio/" + SafeName(avatarRoot.name) + "/" + subFolder;
        }

        public static void RecreateFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        public static bool IsDescendantOrSelf(Transform root, Transform target)
        {
            var current = target;
            while (current != null)
            {
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        public static string GetPathFromAvatarRoot(GameObject avatarRoot, Transform target)
        {
            if (target == avatarRoot.transform) return string.Empty;

            if (!IsDescendantOrSelf(avatarRoot.transform, target))
                throw new InvalidOperationException(target.name + " is not inside the selected avatar hierarchy.");

            string path = target.name;
            var t = target.parent;
            while (t != null && t != avatarRoot.transform)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        // ------------------------------------------------------------------
        // Audio source
        // ------------------------------------------------------------------

        public struct FalloffSettings
        {
            public float NearRadius;
            public float FarRadius;
            public float CloseVolume;
            public float FarVolume;
            public float Body;
            public float LowPassAtFar;

            public static FalloffSettings Default
            {
                get
                {
                    return new FalloffSettings
                    {
                        NearRadius = 1.5f,
                        FarRadius = 18f,
                        CloseVolume = 0.85f,
                        FarVolume = 0.08f,
                        Body = 0.55f,
                        LowPassAtFar = 1200f
                    };
                }
            }
        }

        /// <summary>
        /// Volume multiplier over normalised distance (0 = at the source, 1 = Far Radius).
        /// Flat at Close Volume out to the Near Radius, then eased down to Far Volume.
        /// </summary>
        public static AnimationCurve BuildRolloffCurve(FalloffSettings falloff)
        {
            float plateau = Mathf.Clamp(falloff.NearRadius / Mathf.Max(falloff.FarRadius, 0.01f), 0.01f, 0.9f);
            float close = Mathf.Max(falloff.CloseVolume, 0.0001f);
            float far = Mathf.Clamp(falloff.FarVolume, 0f, close);

            // A knee part way down keeps the drop musical rather than linear - most of the loss
            // happens just outside the near radius, which is what "loud up close, quiet across
            // the room" actually sounds like.
            float kneeX = Mathf.Lerp(plateau, 1f, 0.35f);
            float kneeY = Mathf.Lerp(far, close, 0.32f);

            var curve = new AnimationCurve(
                new Keyframe(0f, close),
                new Keyframe(plateau, close),
                new Keyframe(kneeX, kneeY),
                new Keyframe(1f, far));

            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);

            AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
            return curve;
        }

        public static AnimationCurve BuildLowPassCurve(FalloffSettings falloff)
        {
            float plateau = Mathf.Clamp(falloff.NearRadius / Mathf.Max(falloff.FarRadius, 0.01f), 0.01f, 0.9f);
            float far = Mathf.Clamp(falloff.LowPassAtFar, 300f, 22000f);

            var curve = new AnimationCurve(
                new Keyframe(0f, 22000f),
                new Keyframe(plateau, 22000f),
                new Keyframe(1f, far));

            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);

            return curve;
        }

        public static GameObject CreateAudioObject(
            Transform parent,
            string name,
            float volume,
            FalloffSettings falloff,
            bool withLowPass)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Avatar Audio Source");
            go.transform.SetParent(parent, false);

            var source = Undo.AddComponent<AudioSource>(go);
            source.playOnAwake = false;
            source.loop = false; // VRC Animator Play Audio owns looping.
            source.volume = volume;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = falloff.NearRadius;
            source.maxDistance = falloff.FarRadius;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, BuildRolloffCurve(falloff));

            // A little spread stops the source collapsing to a hard point between the ears,
            // which is what makes close-range playback feel thin.
            source.spread = Mathf.Lerp(0f, 45f, falloff.Body);

            if (withLowPass)
            {
                var lowPass = Undo.AddComponent<AudioLowPassFilter>(go);
                lowPass.cutoffFrequency = 22000f;
                lowPass.lowpassResonanceQ = Mathf.Lerp(1f, 2.5f, falloff.Body);
                lowPass.customCutoffCurve = BuildLowPassCurve(falloff);
            }

            var spatial = AddSpatialAudioComponent(go);
            if (spatial != null)
            {
                TrySetMember(spatial, "Gain", 0f);
                TrySetMember(spatial, "Near", falloff.NearRadius);
                TrySetMember(spatial, "Far", falloff.FarRadius);
                TrySetMember(spatial, "VolumetricRadius",
                    Mathf.Min(falloff.NearRadius * falloff.Body, falloff.FarRadius));

                // Critical: without this VRChat throws away the custom rolloff curve above and
                // substitutes its own linear falloff, losing all the near/far shaping.
                TrySetMember(spatial, "UseAudioSourceVolumeCurve", true);
                TrySetMember(spatial, "EnableSpatialization", true);
            }

            return go;
        }

        private static Component AddSpatialAudioComponent(GameObject go)
        {
            // VRCSpatialAudioSource has moved between SDK assemblies over time, so it is resolved
            // at runtime and a missing type only warns rather than failing to compile.
            var spatialType = FindType("VRC.SDK3.Components.VRCSpatialAudioSource");
            if (spatialType == null || spatialType.IsAbstract)
                spatialType = FindType("VRC.SDKBase.VRC_SpatialAudioSource");

            if (spatialType == null || spatialType.IsAbstract || !typeof(Component).IsAssignableFrom(spatialType))
            {
                Debug.LogWarning(
                    "[NekoSuneVR] No concrete VRChat spatial-audio component was found. The AudioSource was " +
                    "still created; the SDK may add its default component during validation. If it does not, " +
                    "update the VRChat SDK - Avatars package in VCC.");
                return null;
            }

            var existing = go.GetComponent(spatialType);
            if (existing != null) return existing;

            var component = go.AddComponent(spatialType);
            Undo.RegisterCreatedObjectUndo(component, "Add VRChat Spatial Audio Source");
            return component;
        }

        // ------------------------------------------------------------------
        // Clips
        // ------------------------------------------------------------------

        public static AnimationClip CreatePhaseClip(
            GameObject avatarRoot,
            string clipName,
            float duration,
            IEnumerable<GameObject> managedObjects,
            params GameObject[] activeObjects)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
                wrapMode = WrapMode.ClampForever
            };

            var activeSet = new HashSet<GameObject>(activeObjects.Where(x => x != null));

            foreach (var target in managedObjects.Where(x => x != null).Distinct())
            {
                string path = GetPathFromAvatarRoot(avatarRoot, target.transform);
                var binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
                var curve = AnimationCurve.Constant(0f, Mathf.Max(0.01f, duration), activeSet.Contains(target) ? 1f : 0f);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            return clip;
        }

        // ------------------------------------------------------------------
        // Controller
        // ------------------------------------------------------------------

        public static void AddBoolParameter(AnimatorController controller, string name, bool defaultValue)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = defaultValue
            });
        }

        public static AnimatorStateTransition AddTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            string parameter,
            bool hasExitTime = false,
            float exitTime = 1f)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(mode, 0f, parameter);
            return transition;
        }

        public static void AddPlayAudioBehaviour(
            AnimatorState state,
            string sourcePath,
            AudioClip clip,
            float volume,
            bool loop)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state), "Cannot add Animator Play Audio to a null state.");

            if (clip == null)
            {
                Debug.LogWarning("[NekoSuneVR] Skipping Animator Play Audio because the AudioClip is null.");
                return;
            }

            VRCAnimatorPlayAudio playAudio = null;

            // Normal path. On some Unity/SDK combinations this returns null even though the
            // controller is fine, so the result is verified before use.
            try
            {
                playAudio = state.AddStateMachineBehaviour<VRCAnimatorPlayAudio>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoSuneVR] Falling back to manual Play Audio creation: " + ex.Message);
            }

            if (playAudio == null && state.behaviours != null)
                playAudio = state.behaviours.OfType<VRCAnimatorPlayAudio>().LastOrDefault();

            if (playAudio == null)
            {
                // StateMachineBehaviours are ScriptableObjects and must live inside the controller
                // asset to survive serialization.
                playAudio = ScriptableObject.CreateInstance<VRCAnimatorPlayAudio>();
                if (playAudio == null)
                    throw new InvalidOperationException(
                        "Unity could not create VRCAnimatorPlayAudio. Reinstall the VRChat Avatars SDK.");

                playAudio.name = "VRC Animator Play Audio";
                playAudio.hideFlags = HideFlags.HideInHierarchy;

                string controllerPath = AssetDatabase.GetAssetPath(state);
                if (string.IsNullOrEmpty(controllerPath))
                {
                    UnityEngine.Object.DestroyImmediate(playAudio);
                    throw new InvalidOperationException(
                        "The AnimatorState is not saved into a controller asset yet. Delete the generated " +
                        "folder and run the installer again.");
                }

                AssetDatabase.AddObjectToAsset(playAudio, controllerPath);

                var behaviours = state.behaviours ?? Array.Empty<StateMachineBehaviour>();
                ArrayUtility.Add(ref behaviours, playAudio);
                state.behaviours = behaviours;
                EditorUtility.SetDirty(state);
            }

            playAudio.SourcePath = sourcePath ?? string.Empty;
            playAudio.Clips = new[] { clip };
            playAudio.PlaybackOrder = VRCAnimatorPlayAudio.Order.Random;
            playAudio.Volume = new Vector2(volume, volume);
            playAudio.Pitch = Vector2.one;
            playAudio.Loop = loop;

            // Apply everything explicitly after StopOnEnter, so an intro clip and an AFK clip can
            // share one AudioSource without inheriting settings from the previous state.
            playAudio.VolumeApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
            playAudio.PitchApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
            playAudio.ClipsApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
            playAudio.LoopApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;

            playAudio.DelayInSeconds = 0f;
            playAudio.StopOnEnter = true;
            playAudio.PlayOnEnter = true;
            playAudio.PlayOnExit = false;
            playAudio.StopOnExit = true;

            EditorUtility.SetDirty(playAudio);
        }

        // ------------------------------------------------------------------
        // Expression menu
        // ------------------------------------------------------------------

        public struct MenuToggle
        {
            public string Parameter;
            public string Label;
        }

        public static void BuildMenuAssets(
            string outputFolder,
            string folderName,
            IList<MenuToggle> toggles,
            bool defaultOn,
            bool saved,
            out VRCExpressionsMenu rootMenu,
            out VRCExpressionParameters parameters)
        {
            rootMenu = null;
            parameters = null;

            if (toggles == null || toggles.Count == 0)
                return;

            var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            subMenu.name = "NekoAudio_Menu";
            subMenu.controls = toggles.Select(toggle => new VRCExpressionsMenu.Control
            {
                name = toggle.Label,
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = toggle.Parameter },
                value = 1f
            }).ToList();

            AssetDatabase.CreateAsset(subMenu, outputFolder + "/NekoAudio_Menu.asset");

            var root = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            root.name = "NekoAudio_MenuRoot";
            root.controls = new List<VRCExpressionsMenu.Control>
            {
                new VRCExpressionsMenu.Control
                {
                    name = string.IsNullOrWhiteSpace(folderName) ? "Neko Audio" : folderName.Trim(),
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = subMenu,
                    parameter = new VRCExpressionsMenu.Control.Parameter()
                }
            };
            AssetDatabase.CreateAsset(root, outputFolder + "/NekoAudio_MenuRoot.asset");

            var prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            prms.name = "NekoAudio_Parameters";
            prms.parameters = toggles.Select(toggle => new VRCExpressionParameters.Parameter
            {
                name = toggle.Parameter,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = defaultOn ? 1f : 0f,
                saved = saved,
                networkSynced = true
            }).ToArray();
            AssetDatabase.CreateAsset(prms, outputFolder + "/NekoAudio_Parameters.asset");

            AssetDatabase.SaveAssets();

            rootMenu = root;
            parameters = prms;
        }

        // ------------------------------------------------------------------
        // Backends
        // ------------------------------------------------------------------

        public static void Install(
            InstallBackend backend,
            GameObject root,
            AnimatorController controller,
            VRCExpressionsMenu menu,
            VRCExpressionParameters parameters,
            IList<MenuToggle> toggles,
            bool defaultOn,
            bool saved)
        {
            if (backend == InstallBackend.VRCFury)
                InstallWithVRCFury(root, controller, menu, parameters);
            else
                InstallWithModularAvatar(root, controller, menu, toggles, defaultOn, saved);
        }

        private static void InstallWithVRCFury(
            GameObject root,
            AnimatorController controller,
            VRCExpressionsMenu menu,
            VRCExpressionParameters parameters)
        {
            var furyType = FindType("com.vrcfury.api.FuryComponents");
            if (furyType == null)
                throw new InvalidOperationException("VRCFury was not found. Install or update VRCFury and try again.");

            var create = furyType.GetMethod(
                "CreateFullController",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(GameObject) },
                null);

            if (create == null)
                throw new InvalidOperationException(
                    "This VRCFury version does not expose CreateFullController(GameObject). Update VRCFury.");

            var full = create.Invoke(null, new object[] { root });
            if (full == null)
                throw new InvalidOperationException("VRCFury failed to create its Full Controller component.");

            var addController = full.GetType().GetMethods().FirstOrDefault(m =>
            {
                if (m.Name != "AddController") return false;
                var p = m.GetParameters();
                return p.Length == 2
                       && typeof(RuntimeAnimatorController).IsAssignableFrom(p[0].ParameterType)
                       && p[1].ParameterType == typeof(VRCAvatarDescriptor.AnimLayerType);
            });

            if (addController == null)
                throw new InvalidOperationException(
                    "This VRCFury version does not expose the expected AddController API. Update VRCFury.");

            addController.Invoke(full, new object[] { controller, VRCAvatarDescriptor.AnimLayerType.FX });

            if (menu == null || parameters == null)
                return;

            var addMenu = full.GetType().GetMethods().FirstOrDefault(m =>
            {
                if (m.Name != "AddMenu") return false;
                var p = m.GetParameters();
                return p.Length >= 1 && typeof(VRCExpressionsMenu).IsAssignableFrom(p[0].ParameterType);
            });

            var addParams = full.GetType().GetMethods().FirstOrDefault(m =>
            {
                if (m.Name != "AddParams") return false;
                var p = m.GetParameters();
                return p.Length == 1 && typeof(VRCExpressionParameters).IsAssignableFrom(p[0].ParameterType);
            });

            if (addMenu == null || addParams == null)
            {
                Debug.LogWarning(
                    "[NekoSuneVR] This VRCFury version has no AddMenu/AddParams API. The FX controller was " +
                    "installed; add the generated menu and parameter assets to the avatar yourself.");
                return;
            }

            addParams.Invoke(full, new object[] { parameters });
            addMenu.Invoke(
                full,
                addMenu.GetParameters().Length == 1
                    ? new object[] { menu }
                    : new object[] { menu, string.Empty });
        }

        private static void InstallWithModularAvatar(
            GameObject root,
            AnimatorController controller,
            VRCExpressionsMenu menu,
            IList<MenuToggle> toggles,
            bool defaultOn,
            bool saved)
        {
            var mergeType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator");
            if (mergeType == null)
                throw new InvalidOperationException("Modular Avatar was not found. Install or update it and try again.");

            var merge = Undo.AddComponent(root, mergeType);
            if (merge == null)
                throw new InvalidOperationException("Could not add Modular Avatar Merge Animator.");

            SetMember(merge, "animator", controller);
            SetMember(merge, "layerType", VRCAvatarDescriptor.AnimLayerType.FX);
            SetEnumMember(merge, "pathMode", "Absolute");
            SetMember(merge, "matchAvatarWriteDefaults", true);
            SetMember(merge, "deleteAttachedAnimator", false);
            EditorUtility.SetDirty(merge);

            if (menu == null || toggles == null || toggles.Count == 0)
                return;

            try
            {
                var installerType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstaller");
                var parametersType = FindType("nadena.dev.modular_avatar.core.ModularAvatarParameters");
                var configType = FindType("nadena.dev.modular_avatar.core.ParameterConfig");

                if (installerType == null || parametersType == null || configType == null)
                    throw new InvalidOperationException("Modular Avatar menu/parameter types were not found.");

                var installer = Undo.AddComponent(root, installerType);
                SetMember(installer, "menuToAppend", menu);
                EditorUtility.SetDirty(installer);

                var maParameters = Undo.AddComponent(root, parametersType);

                var listType = typeof(List<>).MakeGenericType(configType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType);

                foreach (var toggle in toggles)
                {
                    object config = Activator.CreateInstance(configType);
                    TrySetMember(config, "nameOrPrefix", toggle.Parameter);
                    TrySetMember(config, "defaultValue", defaultOn ? 1f : 0f);
                    TrySetMember(config, "saved", saved);
                    TrySetMember(config, "internalParameter", false);
                    TrySetMember(config, "isPrefix", false);
                    TrySetMember(config, "localOnly", false);
                    SetEnumMember(config, "syncType", "Bool");
                    list.Add(config);
                }

                SetMember(maParameters, "parameters", list);
                EditorUtility.SetDirty(maParameters);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[NekoSuneVR] Could not attach the Modular Avatar menu/parameter components. The FX " +
                    "controller is installed; add the generated menu asset manually. " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Doppel ScreenFX
        // ------------------------------------------------------------------

        public static Shader FindDoppelShader()
        {
            var exact = Shader.Find(DoppelShaderName235);
            if (exact != null) return exact;

            // Future-proof for later Dope Shader releases: accept anything under the same
            // canonical ScreenFX shader name.
            foreach (string query in new[] { "DopeShader t:Shader", "Dope Shader t:Shader" })
            {
                foreach (string guid in AssetDatabase.FindAssets(query))
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                    if (shader != null &&
                        shader.name.StartsWith("Doppels shaders/ScreenFX/Dope Shader", StringComparison.OrdinalIgnoreCase))
                        return shader;
                }
            }

            return null;
        }

        public static string FindAssetPathByFileName(string exactFileName, string typeFilter)
        {
            string searchName = Path.GetFileNameWithoutExtension(exactFileName).Replace("[", "").Replace("]", "");

            string[] queries = { searchName + " " + typeFilter, "SFX " + typeFilter, "Dope " + typeFilter };

            foreach (string query in queries)
            {
                foreach (string guid in AssetDatabase.FindAssets(query))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetFileName(path), exactFileName, StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            // Last pass over everything of that type, in case AssetDatabase tokenisation changes.
            foreach (string guid in AssetDatabase.FindAssets(typeFilter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), exactFileName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }

        public static GameObject InstantiateDoppelTemplate(string exactPrefabName, Transform parent, string outputName)
        {
            string path = FindAssetPathByFileName(exactPrefabName + ".prefab", "t:Prefab");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[NekoSuneVR] Could not find Doppel template prefab: " + exactPrefabName);
                return null;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) return null;

            Undo.RegisterCreatedObjectUndo(instance, "Create " + outputName);
            instance.name = outputName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Doppel's own menu unpacks the prefab after instantiation.
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            instance.SetActive(false);
            return instance;
        }

        public static GameObject CreateDoppelEffect(
            Transform parent,
            string outputFolder,
            Shader shader,
            string objectName,
            DoppelPreset preset,
            DoppelRendererMode rendererMode)
        {
            string template = rendererMode == DoppelRendererMode.ParticleSystem
                ? "[PREFAB] SFX Particle System"
                : "[PREFAB] SFX Skinned Mesh Renderer";

            var effect = InstantiateDoppelTemplate(template, parent, objectName);
            if (effect == null) return null;

            var mat = new Material(shader) { name = objectName + "_DopeMaterial" };
            ApplyDoppelPreset(mat, preset, rendererMode == DoppelRendererMode.ParticleSystem);

            AssetDatabase.CreateAsset(mat, outputFolder + "/" + mat.name + ".mat");

            if (rendererMode == DoppelRendererMode.ParticleSystem)
            {
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                    throw new InvalidOperationException("Doppel Particle System template had no ParticleSystemRenderer.");

                renderer.sharedMaterial = mat;

                // Exactly what DopeSFXGUI's "Configure Particle Systems Renderer" button applies.
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position,
                    ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.Center
                });
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

                var ps = effect.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.loop = true;
                    main.playOnAwake = true;
                }
            }
            else
            {
                var renderer = effect.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null)
                    throw new InvalidOperationException("Doppel Skinned Mesh template had no SkinnedMeshRenderer.");
                renderer.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(effect);
            effect.SetActive(false);
            return effect;
        }

        private static void ApplyDoppelPreset(Material mat, DoppelPreset preset, bool particleRenderer)
        {
            SetMat(mat, "UFPS", particleRenderer ? 1f : 0f);
            SetMat(mat, "DF", 0f);
            SetMat(mat, "_fs", 0.35f);
            SetMat(mat, "_fe", 3.25f);
            SetMat(mat, "SM", 1f);

            SetMat(mat, "FILMGRAIN", 1f);
            SetMat(mat, "VIGNETTE", 1f);
            SetMat(mat, "LINES", 1f);
            SetMat(mat, "_Vignetteround", 1f);

            if (preset == DoppelPreset.DigitalBoot)
            {
                SetMat(mat, "GLITCH", 1f);
                SetMat(mat, "_gtotal", 36f);
                SetMat(mat, "_gspeed", 18f);
                SetMat(mat, "_blocksize", 0.28f);
                SetMat(mat, "_glitchoffset", 0.22f);
                SetMat(mat, "_coloffset", 0.18f);
                SetMat(mat, "RGB", 1f);
                SetMat(mat, "_rgboffs", 0.012f);
                SetMat(mat, "ZOOM", 1f);
                SetMat(mat, "_Zoom", 0.018f);
                SetMat(mat, "_ZoomSpeed", 1.4f);
                SetMat(mat, "SHAKE", 1f);
                SetMat(mat, "_Totalamp", 0.018f);
                SetMat(mat, "NOISE", 1f);
                SetMat(mat, "_Noise", 0.08f);
                SetMat(mat, "_Noisep", 0.55f);
                SetMat(mat, "_fga", 0.12f);
                SetMat(mat, "_Vignette", 0.22f);
                SetMat(mat, "_Vignettesmtf", 0.55f);
                SetMat(mat, "_lines", 0.12f);
                SetMat(mat, "_linesc", 46f);
                SetMat(mat, "_liness", 2.4f);
                SetMatColor(mat, "_linescol", new Color(0.2f, 0.9f, 1f, 0.22f));
            }
            else
            {
                SetMat(mat, "GLITCH", 2f);
                SetMat(mat, "_gtotal", 52f);
                SetMat(mat, "_gspeed", 10f);
                SetMat(mat, "_blocksize", 0.42f);
                SetMat(mat, "_glitchoffset", 0.35f);
                SetMat(mat, "_coloffset", 0.24f);
                SetMat(mat, "RGB", 2f);
                SetMat(mat, "_rgboffs", 0.018f);
                SetMat(mat, "PIX", 1f);
                SetMat(mat, "_pix", 7f);
                SetMat(mat, "PIXAUTO", 1f);
                SetMat(mat, "WAVES", 1f);
                SetMat(mat, "_Waves", 1.2f);
                SetMat(mat, "_WavesPower", 1.8f);
                SetMat(mat, "_WavesSpeed", 2.2f);
                SetMat(mat, "NOISE", 2f);
                SetMat(mat, "_Noise", 0.11f);
                SetMat(mat, "_Noisep", 0.52f);
                SetMat(mat, "_fga", 0.18f);
                SetMat(mat, "_Vignette", 0.34f);
                SetMat(mat, "_Vignettesmtf", 0.48f);
                SetMat(mat, "_lines", 0.18f);
                SetMat(mat, "_linesc", 38f);
                SetMat(mat, "_liness", 1.25f);
                SetMatColor(mat, "_linescol", new Color(0.65f, 0.35f, 1f, 0.18f));
            }
        }

        private static void SetMat(Material mat, string property, float value)
        {
            if (mat != null && mat.HasProperty(property))
                mat.SetFloat(property, value);
        }

        private static void SetMatColor(Material mat, string property, Color value)
        {
            if (mat != null && mat.HasProperty(property))
                mat.SetColor(property, value);
        }

        // ------------------------------------------------------------------
        // Generated boot chime
        // ------------------------------------------------------------------

        public static AudioClip GenerateBootChime(string outputFolder)
        {
            const int sampleRate = 44100;
            const float duration = 1.65f;

            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var pcm = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float sample = 0f;

                sample += SynthNote(t, 0.00f, 0.48f, 440.00f, 0.34f); // A4
                sample += SynthNote(t, 0.34f, 0.58f, 554.37f, 0.30f); // C#5
                sample += SynthNote(t, 0.74f, 0.82f, 659.25f, 0.28f); // E5
                sample += SynthNote(t, 1.08f, 0.52f, 880.00f, 0.18f); // A5 shimmer

                if (t > 0.05f && t < 0.22f)
                {
                    float sparkle = Mathf.Sin(((t - 0.05f) / 0.17f) * Mathf.PI);
                    sample += Mathf.Sin(2f * Mathf.PI * (1350f + 900f * t) * t) * sparkle * 0.035f;
                }

                pcm[i] = (short)Mathf.RoundToInt(Mathf.Clamp(sample, -0.92f, 0.92f) * short.MaxValue);
            }

            string assetPath = outputFolder + "/NekoSune_DigitalBootChime.wav";
            string absolutePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int dataLength = pcm.Length * sizeof(short);
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * sizeof(short));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);
                foreach (short sample in pcm)
                    writer.Write(sample);
            }

            AssetDatabase.ImportAsset(assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
                throw new InvalidOperationException("Generated the intro WAV but Unity could not import it.");

            return clip;
        }

        private static float SynthNote(float time, float start, float length, float frequency, float gain)
        {
            float local = time - start;
            if (local < 0f || local > length) return 0f;

            float attack = Mathf.Clamp01(local / 0.045f);
            float release = Mathf.Clamp01((length - local) / 0.18f);

            float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * local);
            float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * local) * 0.18f;

            return (fundamental + harmonic) * attack * release * gain;
        }
    }
}
#endif

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>Switches an avatar FBX to Rig → Humanoid and points out suspicious auto-mapped bones.</summary>
    public static class ModelRigSetup
    {
        private static readonly string[] OptionalBones = { "Neck", "LeftShoulder", "RightShoulder", "UpperChest", "LeftToes", "RightToes" };

        public static void MakeHumanoid(GameObject model, ConversionLog log)
        {
            string path = AssetDatabase.GetAssetPath(model);
            string file = Path.GetFileName(path);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                log.Error(L.T("That isn't a model file. Drag the .fbx from the Project window, not a scene object."));
                return;
            }

            if (importer.animationType == ModelImporterAnimationType.Human)
            {
                log.Info(L.T("{0}: already Humanoid.", file));
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
                log.Info(L.T("{0}: rig set to Humanoid.", file));
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                log.Error(L.T("Unity couldn't auto-map the bones. Open the FBX → Rig → Configure... and fix the red bones."));
                return;
            }

            var mapped = importer.humanDescription.human.Select(b => b.humanName).ToHashSet();
            var missing = OptionalBones.Where(b => !mapped.Contains(b)).ToList();
            log.Info(L.T("Humanoid avatar is valid.") + (missing.Count > 0 ? " " + L.T("Optional bones not mapped: {0}", string.Join(", ", missing)) : ""));

            // The auto-mapper sometimes grabs a breast/hair bone as Chest, or a random bone as Jaw.
            foreach (var bone in importer.humanDescription.human)
            {
                if (bone.humanName == "Jaw" || Regex.IsMatch(bone.boneName, "(?i)breast|hair|skirt|ear|tail"))
                    log.Warn(L.T("Check mapping: {0} → {1}", bone.humanName, bone.boneName));
            }
        }
    }
}

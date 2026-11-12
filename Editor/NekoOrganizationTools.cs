using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NekoUnityEssentials
{
    public static class NekoOrganizationTools
    {
        [MenuItem("GameObject/Neko Essentials/Group Selected Objects", false, 0)]
        internal static void GroupSelected()
        {
            var selected = Selection.transforms;
            if (selected.Length == 0) return;
            var parent = selected[0].parent;
            var group = new GameObject("--- New Group ---");
            Undo.RegisterCreatedObjectUndo(group, "Create Neko group");
            Undo.SetTransformParent(group.transform, parent, "Parent Neko group");
            foreach (var child in selected)
                if (child && child != group.transform) Undo.SetTransformParent(child, group.transform, "Group objects");
            Selection.activeGameObject = group;
            if (group.scene.IsValid()) EditorSceneManager.MarkSceneDirty(group.scene);
        }

        [MenuItem("Assets/Neko Essentials/Create Standard Folders", false, 30)]
        internal static void CreateFolders()
        {
            var selected = Selection.activeObject;
            var path = selected ? AssetDatabase.GetAssetPath(selected) : "Assets";
            if (string.IsNullOrEmpty(path)) path = "Assets";
            if (!AssetDatabase.IsValidFolder(path)) path = Path.GetDirectoryName(path).Replace('\\', '/');
            foreach (var name in new[] { "Animations", "Materials", "Prefabs", "Scripts", "Textures", "Audio", "Models", "Editor" })
                if (!AssetDatabase.IsValidFolder(path + "/" + name)) AssetDatabase.CreateFolder(path, name);
            AssetDatabase.Refresh();
        }
    }
}

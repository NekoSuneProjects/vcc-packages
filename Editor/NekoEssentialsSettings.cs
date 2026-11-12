using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoUnityEssentials
{
    public enum NekoMatchMode { Contains, StartsWith, Exact }

    [Serializable]
    public class NekoStyleRule
    {
        public bool enabled = true;
        public string match = "Enemies";
        public NekoMatchMode matchMode = NekoMatchMode.Contains;
        public Color background = new Color(0.55f, 0.07f, 0.14f, 0.78f);
        public Color foreground = Color.white;
        public string badge = "NPC";
        public Texture2D icon;
        public bool bold = true;
        public bool applyToChildren;
        public int priority;

        public bool Matches(string name)
        {
            if (!enabled || string.IsNullOrEmpty(match) || name == null) return false;
            switch (matchMode)
            {
                case NekoMatchMode.Exact: return string.Equals(name, match, StringComparison.OrdinalIgnoreCase);
                case NekoMatchMode.StartsWith: return name.StartsWith(match, StringComparison.OrdinalIgnoreCase);
                default: return name.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }

    public class NekoEssentialsSettings : ScriptableObject
    {
        public bool hierarchyEnabled = true;
        public bool foldersEnabled = true;
        public bool showObjectCounts = false;
        public List<NekoStyleRule> hierarchyRules = new List<NekoStyleRule>();
        public List<NekoStyleRule> folderRules = new List<NekoStyleRule>();

        public const string AssetPath = "Assets/Editor/NekoUnityEssentialsSettings.asset";
        private static NekoEssentialsSettings instance;
        public static NekoEssentialsSettings Instance
        {
            get
            {
                if (instance) return instance;
                instance = AssetDatabase.LoadAssetAtPath<NekoEssentialsSettings>(AssetPath);
                if (instance) return instance;
                var ids = AssetDatabase.FindAssets("t:NekoEssentialsSettings");
                if (ids.Length > 0) instance = AssetDatabase.LoadAssetAtPath<NekoEssentialsSettings>(AssetDatabase.GUIDToAssetPath(ids[0]));
                return instance;
            }
        }

        public static NekoEssentialsSettings CreateOrLoad()
        {
            if (Instance) return Instance;
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            var asset = CreateInstance<NekoEssentialsSettings>();
            asset.hierarchyRules.Add(new NekoStyleRule { match = "---", background = new Color(1f, .56f, .03f, .92f), foreground = Color.black, badge = "GROUP" });
            asset.hierarchyRules.Add(new NekoStyleRule { match = "Enemy", background = new Color(.59f, .02f, .08f, .83f), badge = "NPC" });
            asset.hierarchyRules.Add(new NekoStyleRule { match = "Camera", background = new Color(.08f, .36f, .6f, .64f), badge = "CAM" });
            asset.hierarchyRules.Add(new NekoStyleRule { match = "UI", background = new Color(.65f, .51f, .05f, .6f), foreground = Color.white, badge = "UI" });
            asset.folderRules.Add(new NekoStyleRule { match = "Animations", background = new Color(.55f, .24f, .75f, .45f), badge = "ANIM" });
            asset.folderRules.Add(new NekoStyleRule { match = "Scripts", background = new Color(.1f, .48f, .71f, .48f), badge = "CODE" });
            asset.folderRules.Add(new NekoStyleRule { match = "Materials", background = new Color(.12f, .65f, .42f, .45f), badge = "MAT" });
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            instance = asset;
            return asset;
        }

        public static NekoStyleRule FindRule(List<NekoStyleRule> rules, string name)
        {
            if (rules == null) return null;
            NekoStyleRule best = null;
            foreach (var rule in rules)
                if (rule != null && rule.Matches(name) && (best == null || rule.priority > best.priority)) best = rule;
            return best;
        }
    }
}

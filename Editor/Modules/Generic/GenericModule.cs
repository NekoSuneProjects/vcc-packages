using System;
using UnityEditor;
using UnityEngine;

namespace MotionConverter.Generic
{
    /// <summary>Any Generic (Transform-curve) AnimationClip, e.g. the .anim that comes with an exported FBX.</summary>
    [Serializable]
    public sealed class GenericModule : ClipPipelineModule
    {
        [SerializeField] private AnimationClip sourceClip;

        public override string Id => "generic";
        public override string TabName => L.T("Generic FBX / .anim");
        public override int Order => 0;
        public override string Subtitle => L.T("Generic FBX + .anim clip  →  Humanoid");
        public override string Description => L.T("Convert any Generic Unity animation clip (e.g. the clip inside an exported FBX) into a Humanoid motion.");

        protected override void PrepareSource()
        {
            settings.sourceClip = sourceClip;
            settings.sourceAssetPath = null;
        }

        protected override void DrawSourceInput()
        {
            sourceClip = (AnimationClip)EditorGUILayout.ObjectField(L.T("Source clip"), sourceClip, typeof(AnimationClip), false);
            settings.sourceClip = sourceClip;
            DrawClipFacts();
        }
    }
}

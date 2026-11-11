#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.BeatKeyframing
{
    /// <summary>
    /// The NekoSuneVR footer shown as a small popup from the toolbar's "NS" button, so the
    /// visualizer keeps its full height for the timeline.
    /// </summary>
    internal sealed class NekoAboutPopup : PopupWindowContent
    {
        public override Vector2 GetWindowSize()
        {
            return new Vector2(470f, 64f);
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.Space(4f);
            NekoAboutFooter.Draw();
            GUILayout.Label("Beat Keyframing v" + UpdateChecker.CurrentVersion, EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndArea();
        }
    }
}
#endif

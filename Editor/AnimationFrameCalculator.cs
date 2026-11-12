using System;
using UnityEditor;
using UnityEngine;
using S = NekoUnityEssentials.EssentialsStyles;
using G = NekoUnityEssentials.EssentialsGUI;

namespace NekoUnityEssentials
{
    /// <summary>
    /// Frame Calculator tab: how many frames a clip takes when recorded at a given FPS, and the
    /// start/end frames to type into a recorder (e.g. Unity Recorder).
    /// </summary>
    [Serializable]
    internal class AnimationFrameCalculator
    {
        private static readonly int[] Presets = { 24, 30, 60, 120 };

        [SerializeField] private AnimationClip clip;
        [SerializeField] private int recordingFPS = 60;
        [SerializeField] private int startFrame;

        public void OnEnable()
        {
            if (!clip && Selection.activeObject is AnimationClip selected) clip = selected;
        }

        public void OnGUI(EditorWindow host)
        {
            G.BeginCard("AnimationClip Icon", "Clip & recording", "Work out the recorder frame range for an animation at your recording frame rate.");
            using (new EditorGUILayout.HorizontalScope())
            {
                clip = (AnimationClip)EditorGUILayout.ObjectField(new GUIContent("Animation file"), clip, typeof(AnimationClip), false);
                using (new EditorGUI.DisabledScope(!(Selection.activeObject is AnimationClip)))
                    if (GUILayout.Button(new GUIContent("Use selected", "Use the clip selected in the Project window."), EditorStyles.miniButton, GUILayout.Width(96)))
                        clip = (AnimationClip)Selection.activeObject;
            }
            recordingFPS = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Recording FPS", "Frame rate you record or render at."), recordingFPS), 1, 1000);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                for (int i = 0; i < Presets.Length; i++)
                {
                    var style = i == 0 ? EditorStyles.miniButtonLeft : i == Presets.Length - 1 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                    bool on = recordingFPS == Presets[i];
                    if (GUILayout.Toggle(on, Presets[i].ToString(), style) && !on) recordingFPS = Presets[i];
                }
            }
            startFrame = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Recorder start frame", "Usually 0. Change it if the recording starts later in the timeline."), startFrame));
            G.EndCard();

            if (!clip)
            {
                G.BeginCard(null, null);
                G.Hint("Pick an animation clip to see its frame range.");
                G.EndCard();
                return;
            }

            float length = clip.length;
            int totalFrames = Mathf.CeilToInt(length * recordingFPS);
            int endFrame = startFrame + Mathf.Max(0, totalFrames - 1);
            int clipFrames = Mathf.RoundToInt(length * clip.frameRate);

            G.BeginCard("d_UnityEditor.AnimationWindow", "Recorder range");
            using (new EditorGUILayout.HorizontalScope())
            {
                BigValue("Start frame", startFrame.ToString());
                BigValue("End frame", endFrame.ToString(), true);
                BigValue("Frames", totalFrames.ToString());
            }
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = S.Accent;
                if (GUILayout.Button(S.Icon("Clipboard", " Copy End Frame"), GUILayout.Height(26))) Copy(endFrame.ToString());
                GUI.backgroundColor = old;
                if (GUILayout.Button(S.Icon("Clipboard", " Copy Range"), GUILayout.Height(26), GUILayout.Width(120))) Copy(startFrame + "-" + endFrame);
            }
            G.EndCard();

            G.BeginCard("console.infoicon", "Clip details");
            Row("Duration", length.ToString("F3") + " s  (" + Timecode(length, recordingFPS) + ")");
            Row("Clip frame rate", clip.frameRate.ToString("0.##") + " fps  ·  " + clipFrames + " frames");
            Row("Recording frame rate", recordingFPS + " fps  ·  " + totalFrames + " frames");
            Row("Loops", clip.isLooping ? "Yes" : "No");
            if (!Mathf.Approximately(length * recordingFPS, Mathf.Round(length * recordingFPS)))
                G.Hint("The clip doesn't end exactly on a recording frame, so the last frame is rounded up.");
            G.EndCard();
        }

        private static void BigValue(string label, string value, bool accent = false)
        {
            using (new EditorGUILayout.VerticalScope(S.RuleCard))
            {
                var valueStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                if (accent) valueStyle.normal.textColor = S.Accent;
                GUILayout.Label(value, valueStyle, GUILayout.Height(28));
                GUILayout.Label(label.ToUpperInvariant(), new GUIStyle(S.GroupLabel) { alignment = TextAnchor.MiddleCenter });
            }
        }

        private static void Row(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private static string Timecode(float seconds, int fps)
        {
            int total = Mathf.CeilToInt(seconds * fps);
            int frames = total % fps;
            int secs = total / fps;
            return string.Format("{0:00}:{1:00}:{2:00}", secs / 60, secs % 60, frames);
        }

        private static void Copy(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
            EditorWindow.focusedWindow?.ShowNotification(new GUIContent("Copied " + text), 1.0);
        }
    }
}

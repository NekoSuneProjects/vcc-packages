using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        private readonly Dictionary<int, Editor> systemEditors = new Dictionary<int, Editor>();
        private readonly HashSet<int> systemOpen = new HashSet<int>();

        /// <summary>Desktop keys VRChat (or most players) already use.</summary>
        private static readonly KeyCode[] VrchatKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Space, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
            KeyCode.C, KeyCode.Z, KeyCode.V, KeyCode.R, KeyCode.Y, KeyCode.Tab, KeyCode.Escape, KeyCode.Return, KeyCode.Backslash,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F12,
        };

        /// <summary>Ready-made system scripts on this panel, plus controllers elsewhere that drive it.</summary>
        private List<UdonSharpBehaviour> SystemsOf(Canvas c)
        {
            var list = c.GetComponentsInChildren<UdonSharpBehaviour>(true)
                .Where(u => !(u is NekoUiAction) && !(u is NekoUiSlider)).ToList();
            list.AddRange(Resources.FindObjectsOfTypeAll<NekoPortableMenu>()
                .Where(p => p && !EditorUtility.IsPersistent(p) && p.menu == c.transform));
            return list.Distinct().ToList();
        }

        private void DrawSystemCard()
        {
            var systems = SystemsOf(panel);
            if (systems.Count == 0) return;
            G.BeginCard("d_Prefab Icon", "System settings", "The Udon scripts behind this panel's ready-made system. Change their settings here or in the Inspector.");
            foreach (var u in systems)
            {
                int id = u.GetInstanceID();
                bool open = systemOpen.Contains(id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    open = EditorGUILayout.Foldout(open, " " + ObjectNames.NicifyVariableName(u.GetType().Name) + (u.gameObject != panel.gameObject ? "  (on " + u.name + ")" : ""), true);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(Loc.T("Select"), EditorStyles.miniButton, GUILayout.Width(50))) { Selection.activeObject = u.gameObject; EditorGUIUtility.PingObject(u); }
                }
                if (open) systemOpen.Add(id); else systemOpen.Remove(id);

                if (u is NekoHotkeys hk) DrawHotkeyHelp(hk);
                if (!open) continue;

                if (!systemEditors.TryGetValue(id, out var editor) || !editor || editor.target != u)
                {
                    if (editor) DestroyImmediate(editor);
                    editor = Editor.CreateEditor(u);
                    systemEditors[id] = editor;
                }
                EditorGUI.indentLevel++;
                try { editor.OnInspectorGUI(); }
                catch (ExitGUIException) { throw; }
                catch (System.Exception e) { G.Report("Couldn't draw the settings here: " + e.Message + " – use the Inspector.", false); }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }
            G.EndCard();
        }

        private static void DrawHotkeyHelp(NekoHotkeys hk)
        {
            if (hk.keys == null) return;
            for (int i = 0; i < hk.keys.Length; i++)
            {
                var key = (KeyCode)hk.keys[i];
                if (VrchatKeys.Contains(key))
                    G.Report(key + " is already used by VRChat (or common VRChat controls). Pick another key.", false);
                if (hk.keys.Count(k => k == hk.keys[i]) > 1 && hk.keys.ToList().IndexOf(hk.keys[i]) == i)
                    G.Report(key + " is used for more than one hotkey.", false);
            }
            G.Hint("Keys are stored as numbers (Udon can't hold KeyCode lists). Common free keys: M, K, L, J, N, P, O, I, U, H, G, 1–0 (top row).");
        }

        private void DrawCommunityPlayers()
        {
            var players = TemplateLibrary.CommunityPlayers();
            if (players.Count == 0) return;
            G.BeginCard("VideoClip Icon", "Video players already in your scene");
            foreach (var (name, component) in players)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(name + "  (" + component.name + ")");
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(Loc.T("Select"), EditorStyles.miniButton, GUILayout.Width(50))) { Selection.activeObject = component.gameObject; EditorGUIUtility.PingObject(component); }
                }
            G.Hint("These players have their own controls and sync. Keep using their UI – the Assistant's video controls are for the stock VRChat players, so they won't fight over sync. You can still link to them from your menus with \"Send an Udon event\".");
            G.EndCard();
        }
    }
}

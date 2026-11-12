using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NekoSune.WorldUiAssistant
{
    internal enum Severity { Error, Warning, Info }

    internal class Finding
    {
        public Severity severity;
        public string title;
        public string detail;
        public UnityEngine.Object[] objects;   // what to select
        public Action fix;                     // null = no automatic fix
        public string fixLabel = "Fix";
    }

    /// <summary>
    /// UI Doctor: common problems in world UI (made by the Assistant or by hand), with one-click fixes.
    /// Every fix is undoable. Fixes that change import settings are labelled clearly.
    /// </summary>
    internal static class UiDoctor
    {
        public const int MaxQuestTexture = 1024;

        public static List<Finding> Scan(IEnumerable<Canvas> canvases)
        {
            var list = new List<Finding>();
            var all = canvases.Where(c => c).ToList();
            foreach (var c in all) ScanCanvas(c, list);
            ScanScene(all, list);
            if (list.Count == 0)
                list.Add(new Finding { severity = Severity.Info, title = "No problems found", detail = "The basics look healthy. Still test with Build & Test in VRChat (desktop and VR) – the editor isn't the real client." });
            return list.OrderBy(f => f.severity).ToList();
        }

        private static void ScanCanvas(Canvas c, List<Finding> list)
        {
            var go = c.gameObject;
            // Measure the real layout (it may not have been calculated yet, e.g. right after creating).
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
            string n = "\"" + c.name + "\": ";

            if (c.renderMode != RenderMode.WorldSpace)
                list.Add(new Finding { severity = Severity.Error, title = n + "Canvas isn't World Space", detail = "World UI must be World Space so it exists in the world.", objects = new UnityEngine.Object[] { c },
                    fix = () => { Undo.RecordObject(c, "Doctor"); c.renderMode = RenderMode.WorldSpace; } });

            if (!go.GetComponent<GraphicRaycaster>())
                list.Add(new Finding { severity = Severity.Error, title = n + "No Graphic Raycaster", detail = "Nothing on this canvas can be clicked.", objects = new UnityEngine.Object[] { go },
                    fix = () => Undo.AddComponent<GraphicRaycaster>(go) });

            if (UiFactory.UiShapeType != null && !go.GetComponent(UiFactory.UiShapeType))
                list.Add(new Finding { severity = Severity.Error, title = n + "No VRCUiShape", detail = "The VRChat laser can't point at this canvas.", objects = new UnityEngine.Object[] { go },
                    fix = () => Undo.AddComponent(go, UiFactory.UiShapeType) });

            if (UiFactory.CvrWrapperType != null && !go.GetComponent(UiFactory.CvrWrapperType))
                list.Add(new Finding { severity = Severity.Warning, title = n + "No CVR Canvas Wrapper", detail = "ChilloutVR needs it to interact with this canvas.", objects = new UnityEngine.Object[] { go },
                    fix = () => Undo.AddComponent(go, UiFactory.CvrWrapperType) });

            var wrongLayer = c.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.layer != UiFactory.UiLayer).Select(t => (UnityEngine.Object)t.gameObject).ToArray();
            if (wrongLayer.Length > 0)
                list.Add(new Finding { severity = Severity.Warning, title = n + wrongLayer.Length + " object(s) not on the UI layer", detail = "VRChat's laser and UI rendering expect world UI on the UI layer.", objects = wrongLayer,
                    fix = () => { Undo.RegisterFullObjectHierarchyUndo(go, "Doctor"); UiFactory.SetLayer(c.transform, UiFactory.UiLayer); } });

            float scale = c.transform.lossyScale.x;
            if (scale > 0.01f)
                list.Add(new Finding { severity = Severity.Warning, title = n + "Very large scale (" + scale.ToString("0.###") + ")", detail = "World canvases usually use scale 0.001 (1 UI unit = 1 mm). Big scales make text blurry and sizes confusing.", objects = new UnityEngine.Object[] { go } });

            var nav = c.GetComponentsInChildren<Selectable>(true).Where(s => s.navigation.mode != Navigation.Mode.None).ToArray();
            if (nav.Length > 0)
                list.Add(new Finding { severity = Severity.Warning, title = n + nav.Length + " control(s) use Unity Navigation", detail = "Keyboard navigation leaves buttons stuck highlighted in VR. Set Navigation to None.", objects = nav.Select(s => (UnityEngine.Object)s.gameObject).ToArray(),
                    fix = () => { foreach (var s in nav) { Undo.RecordObject(s, "Doctor"); s.navigation = new Navigation { mode = Navigation.Mode.None }; } } });

            var tiny = c.GetComponentsInChildren<Selectable>(true).Where(s => s is Button || s is Toggle)
                .Where(s => s.gameObject.activeInHierarchy) // hidden pages aren't laid out, so they can't be measured
                .Where(s => { var r = ((RectTransform)s.transform).rect; return r.width > 0 && (r.width < 60 || r.height < 36); }).ToArray();
            if (tiny.Length > 0)
                list.Add(new Finding { severity = Severity.Warning, title = n + tiny.Length + " small click target(s)", detail = "Under about 60 × 36 UI units is hard to hit with the VRChat laser.", objects = tiny.Select(s => (UnityEngine.Object)s.gameObject).ToArray(),
                    fix = () => { foreach (var s in tiny) { var le = s.GetComponent<LayoutElement>(); if (!le) le = Undo.AddComponent<LayoutElement>(s.gameObject); Undo.RecordObject(le, "Doctor"); le.minHeight = Mathf.Max(le.minHeight, 48); le.minWidth = Mathf.Max(le.minWidth, 80); } },
                    fixLabel = "Make bigger" });

            var smallText = c.GetComponentsInChildren<TMP_Text>(true).Where(t => t.fontSize < 18 && !t.enableAutoSizing).ToArray();
            if (smallText.Length > 0)
                list.Add(new Finding { severity = Severity.Warning, title = n + smallText.Length + " text(s) under size 18", detail = "Hard to read in VR from a normal distance (2 m).", objects = smallText.Select(t => (UnityEngine.Object)t.gameObject).ToArray(),
                    fix = () => { foreach (var t in smallText) { Undo.RecordObject(t, "Doctor"); t.fontSize = 18; } }, fixLabel = "Set to 18" });

            var lowContrast = c.GetComponentsInChildren<TMP_Text>(true).Where(t => t.color.a < 0.35f).ToArray();
            if (lowContrast.Length > 0)
                list.Add(new Finding { severity = Severity.Info, title = n + lowContrast.Length + " nearly invisible text(s)", detail = "Text colour alpha is very low.", objects = lowContrast.Select(t => (UnityEngine.Object)t.gameObject).ToArray() });

            // Decorations that catch laser hits for nothing (not the background, not inside controls or scroll lists).
            var raycast = c.GetComponentsInChildren<Graphic>(true)
                .Where(g => g.raycastTarget && g.gameObject != go && !(g is TMP_SubMeshUI) && !g.GetComponentInParent<Selectable>(true) && !g.GetComponentInParent<ScrollRect>(true) && !g.GetComponent<TMP_InputField>())
                .ToArray();
            if (raycast.Length > 0)
                list.Add(new Finding { severity = Severity.Info, title = n + raycast.Length + " decoration(s) are Raycast Targets", detail = "Text and pictures that aren't clickable shouldn't catch laser hits. Turning it off is also slightly faster.", objects = raycast.Select(g => (UnityEngine.Object)g.gameObject).ToArray(),
                    fix = () => { foreach (var g in raycast) { Undo.RecordObject(g, "Doctor"); g.raycastTarget = false; } } });

            var heavy = c.GetComponentsInChildren<Image>(true).Where(i => i.sprite && i.sprite.texture)
                .Select(i => i.sprite.texture).Distinct()
                .Where(t => Mathf.Max(t.width, t.height) > MaxQuestTexture && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) is TextureImporter).ToArray();
            if (heavy.Length > 0)
                list.Add(new Finding { severity = Severity.Warning, title = n + heavy.Length + " texture(s) bigger than " + MaxQuestTexture + " px", detail = "Heavy for Quest/Android. The fix sets an Android max size of " + MaxQuestTexture + " in the texture import settings (PC is unchanged).", objects = heavy,
                    fix = () => LimitForAndroid(heavy), fixLabel = "Limit on Quest" });

            foreach (var p in UiActions.Check(c))
                list.Add(new Finding { severity = Severity.Error, title = n + "Action: " + p.text, objects = p.where ? new UnityEngine.Object[] { p.where.gameObject } : null, fix = p.fix });

            var pages = UiWidgets.PagesOf(c.transform);
            if (pages.Count > 1 && pages.Count(p => p.gameObject.activeSelf) != 1)
                list.Add(new Finding { severity = Severity.Warning, title = n + pages.Count(p => p.gameObject.activeSelf) + " pages are visible", detail = "Exactly one page should be visible when the world loads (usually the first).", objects = pages.Select(p => (UnityEngine.Object)p.gameObject).ToArray(),
                    fix = () => UiActions.ShowPage(pages[0]), fixLabel = "Show first page" });
        }

        private static void ScanScene(List<Canvas> canvases, List<Finding> list)
        {
            if (canvases.Count > 12)
                list.Add(new Finding { severity = Severity.Info, title = canvases.Count + " world canvases in the scene", detail = "Each canvas costs a little every frame. Merge small signs into shared panels where you can." });

            for (int i = 0; i < canvases.Count; i++)
                for (int j = i + 1; j < canvases.Count; j++)
                {
                    var a = UiFactory.WorldBounds((RectTransform)canvases[i].transform);
                    var b = UiFactory.WorldBounds((RectTransform)canvases[j].transform);
                    if (!a.Intersects(b)) continue;
                    var overlap = OverlapVolume(a, b);
                    if (overlap <= 0) continue;
                    int ci = i, cj = j;
                    list.Add(new Finding { severity = Severity.Warning, title = "\"" + canvases[ci].name + "\" overlaps \"" + canvases[cj].name + "\"", detail = "Overlapping panels flicker (z-fighting) and the laser may hit the wrong one. Move one of them.", objects = new UnityEngine.Object[] { canvases[ci].gameObject, canvases[cj].gameObject } });
                }
        }

        private static float OverlapVolume(Bounds a, Bounds b)
        {
            var min = Vector3.Max(a.min, b.min);
            var max = Vector3.Min(a.max, b.max);
            var size = max - min;
            // Flat panels have ~zero depth: compare the two largest overlap axes.
            var dims = new[] { size.x, size.y, size.z }.OrderByDescending(x => x).ToArray();
            return dims[0] > 0.01f && dims[1] > 0.01f && dims[2] >= -0.005f ? dims[0] * dims[1] : 0f;
        }

        private static void LimitForAndroid(Texture2D[] textures)
        {
            foreach (var t in textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
                var s = importer.GetPlatformTextureSettings("Android");
                s.overridden = true;
                s.maxTextureSize = MaxQuestTexture;
                importer.SetPlatformTextureSettings(s);
                importer.SaveAndReimport();
            }
        }
    }
}

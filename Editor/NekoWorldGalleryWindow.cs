using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NekoSune.WorldGallery.Editor
{
    internal sealed class NekoWorldGalleryWindow : EditorWindow
    {
        const string GeneratedFolder = "Assets/NekoSune/Gallery/Generated";
        const string GeneratedScriptPath = GeneratedFolder + "/NekoImageGalleryRuntime.cs";
        const string GeneratedProgramPath = GeneratedFolder + "/NekoImageGalleryRuntime.asset";

        readonly string[] _effects = { "Cross Fade", "Slide Left", "Slide Right", "Slide Up", "Zoom", "Spin + Zoom", "Shader Wipe", "Shader Dissolve", "Shader Radial" };
        readonly string[] _sources = { "Local Texture[]", "Remote predeclared VRCUrl[]" };
        int _effect;
        int _sourceMode;
        float _duration = 0.65f;
        bool _autoPlay = true;
        float _autoSeconds = 7f;
        Vector2 _scroll;

        string _remoteJsonUrl = "";
        string _rootPath = "items";
        string _titleKey = "title";
        string _subtitleKey = "subtitle";
        string _indexKey = "imageIndex";
        string _urlKey = "imageUrl";
        string _predeclaredUrls = "";
        string _urlAliases = "";

        [MenuItem("NekoSune/World/Image Gallery", false, 24)]
        public static void Open()
        {
            var w = GetWindow<NekoWorldGalleryWindow>(false, "World Image Gallery", true);
            w.minSize = new Vector2(790f, 670f);
            w.Show();
        }

        void OnEnable() { UpdateChecker.Changed += Repaint; }
        void OnDisable() { UpdateChecker.Changed -= Repaint; }

        void OnGUI()
        {
            NekoStyles.Ensure();
            EditorGUILayout.BeginHorizontal();
            NekoStyles.HeaderBar("Image Gallery", "NekoSune", "Stylish galleries for local textures, remote VRCUrls and JSON/path-driven feeds");
            DrawVersionChip();
            EditorGUILayout.EndHorizontal();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawUpdateBanner();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Repair / update generated runtime", NekoStyles.SlotName);
            EditorGUILayout.LabelField("If an older generated NekoImageGalleryRuntime.cs has a compile error, copy the latest package runtime over it without rebuilding the UI.", NekoStyles.WrapLabel);
            if (GUILayout.Button("REPAIR / COPY LATEST GALLERY RUNTIME", NekoStyles.PrimaryButton, GUILayout.Height(34f)))
            {
                CopyRuntime();
                EditorUtility.DisplayDialog("Image Gallery", "Replaced " + GeneratedScriptPath + " with the latest runtime. Let Unity/UdonSharp compile it, then use AUTO-WIRE / REPAIR on your existing gallery root.", "OK");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Gallery source", NekoStyles.SlotName);
            _sourceMode = EditorGUILayout.Popup("Image source", _sourceMode, _sources);
            _effect = EditorGUILayout.Popup("Transition", _effect, _effects);
            _duration = EditorGUILayout.Slider("Transition time", _duration, 0.1f, 2.5f);
            _autoPlay = EditorGUILayout.Toggle("Auto-play", _autoPlay);
            _autoSeconds = EditorGUILayout.Slider("Auto-play seconds", _autoSeconds, 2f, 30f);
            if (GUILayout.Button("BUILD DEMO IMAGE GALLERY", NekoStyles.PrimaryButton, GUILayout.Height(36f))) BuildDemo();
            EditorGUILayout.LabelField("The demo creates three local textures so it works immediately. Switch Image Source to Remote before Auto-Wire if you want the VRCUrl/JSON configuration below applied instead.", NekoStyles.WrapLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Remote JSON mapper", NekoStyles.SlotName);
            _remoteJsonUrl = EditorGUILayout.TextField("Remote JSON URL", _remoteJsonUrl);
            _rootPath = EditorGUILayout.TextField("Root array path", _rootPath);
            _titleKey = EditorGUILayout.TextField("Title field", _titleKey);
            _subtitleKey = EditorGUILayout.TextField("Subtitle field", _subtitleKey);
            _indexKey = EditorGUILayout.TextField("Image index field", _indexKey);
            _urlKey = EditorGUILayout.TextField("Image URL/path field", _urlKey);
            EditorGUILayout.HelpBox("Root Array Path supports a root array, a normal key such as items, or a dotted path such as payload.gallery.images. Array entries may be objects, numeric image indexes, or raw strings such as /gallery/a.png.", MessageType.Info);

            GUILayout.Label("Predeclared image URLs — one per line", EditorStyles.boldLabel);
            _predeclaredUrls = EditorGUILayout.TextArea(_predeclaredUrls, GUILayout.MinHeight(72f));
            GUILayout.Label("Optional aliases / relative paths — one per line, aligned with URLs", EditorStyles.boldLabel);
            _urlAliases = EditorGUILayout.TextArea(_urlAliases, GUILayout.MinHeight(58f));
            EditorGUILayout.LabelField("Example: URL https://cdn.example.com/gallery/a.png can use alias /gallery/a.png. JSON may then contain only that path and still map to the predeclared VRCUrl safely.", NekoStyles.WrapLabel);
            if (GUILayout.Button("Copy example gallery JSON into Assets")) CopyExampleJson();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("After UdonSharp compiles", NekoStyles.SlotName);
            EditorGUILayout.LabelField("Keep the generated or existing gallery root selected and click Auto-Wire / Repair. It verifies the UdonSharpProgramAsset before attaching the runtime, fills UI/data references and wires the buttons.", NekoStyles.WrapLabel);
            if (GUILayout.Button("AUTO-WIRE / REPAIR SELECTED GALLERY", NekoStyles.PrimaryButton, GUILayout.Height(34f))) AutoWire(Selection.activeGameObject);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Transition library", NekoStyles.SlotName);
            EditorGUILayout.LabelField("Cross-fade, slide left/right/up, zoom, spin+zoom, plus shader wipe, dissolve and radial reveal. The runtime uses two RawImage layers for transform effects and a third shader layer for the GPU effects.", NekoStyles.WrapLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.HelpBox("VRChat still does not let Udon construct arbitrary VRCUrl objects from downloaded JSON strings. NekoSune maps downloaded full URLs, relative paths or aliases onto creator-predeclared VRCUrl[] entries instead of bypassing that security model.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        // ------------------------------------------------------------------ update checker

        /// <summary>Version chip right of the header. Amber "v1.1.0 → v1.2.0" when an update is out; click to check now.</summary>
        static void DrawVersionChip()
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking: text = current + "  ···"; tip = "Checking for updates..."; break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Update available: v" + UpdateChecker.LatestVersion + "\nClick to check for updates.";
                    break;
                case UpdateChecker.State.UpToDate: tip = "You're up to date.\nClick to check for updates."; break;
                case UpdateChecker.State.Failed: tip = "Couldn't check for updates (offline?). Click to try again."; break;
                default: tip = "Click to check for updates."; break;
            }

            var style = new GUIStyle(NekoStyles.Chip) { fontStyle = update ? FontStyle.Bold : FontStyle.Normal };
            if (update) style.normal.textColor = NekoStyles.Warn;

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            var content = new GUIContent(text, tip);
            Rect r = GUILayoutUtility.GetRect(content, style, GUILayout.ExpandWidth(false));
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, content, style)) UpdateChecker.Check();
            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        /// <summary>Card shown at the top when a newer version is out.</summary>
        static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Update available: v" + UpdateChecker.LatestVersion, NekoStyles.SlotName);
            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, NekoStyles.Warn);
            EditorGUILayout.LabelField("You have v" + UpdateChecker.CurrentVersion + ". Update to get the latest fixes and features.", NekoStyles.WrapLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes)) EditorGUILayout.LabelField(UpdateChecker.Notes, NekoStyles.WrapLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Download update", NekoStyles.PrimaryButton, GUILayout.Height(26f))) UpdateChecker.OpenDownload();
            if (GUILayout.Button("Later", GUILayout.Height(26f), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
            if (GUILayout.Button("Skip this version", GUILayout.Height(26f), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void BuildDemo()
        {
            string assetFolder = EnsureFolder(GeneratedFolder);
            Texture2D[] demo = CreateDemoTextures(assetFolder);
            Material transitionMaterial = CreateTransitionMaterial(assetFolder);

            GameObject root = new GameObject("Neko Image Gallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            Undo.RegisterCreatedObjectUndo(root, "Create Neko Image Gallery");
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rootRt = root.GetComponent<RectTransform>(); rootRt.sizeDelta = new Vector2(1100f, 760f); root.transform.localScale = Vector3.one * 0.001f;
            root.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.07f, 0.98f);
            TryAddVrcUiShape(root);

            GameObject frame = Ui("[Frame]", root.transform, typeof(Image)); Stretch(frame.GetComponent<RectTransform>(), 28f); frame.GetComponent<Image>().color = new Color(0.075f,0.09f,0.135f,1f);
            GameObject viewport = Ui("[Viewport]", frame.transform, typeof(RectMask2D)); RectTransform vr = viewport.GetComponent<RectTransform>(); vr.anchorMin = new Vector2(0f,0.18f); vr.anchorMax = new Vector2(1f,1f); vr.offsetMin = new Vector2(22f,0f); vr.offsetMax = new Vector2(-22f,-78f);

            RawImage front = Raw(viewport.transform, "[FrontImage]"); RawImage back = Raw(viewport.transform, "[BackImage]"); RawImage fx = Raw(viewport.transform, "[ShaderImage]");
            Stretch(front.rectTransform, 0f); Stretch(back.rectTransform, 0f); Stretch(fx.rectTransform, 0f);
            front.texture = demo[0]; back.texture = demo[1];
            CanvasGroup fg = front.gameObject.AddComponent<CanvasGroup>(); CanvasGroup bg = back.gameObject.AddComponent<CanvasGroup>(); bg.alpha = 0f;
            fx.gameObject.SetActive(false); if (transitionMaterial != null) fx.material = transitionMaterial;

            Text title = TextNode(frame.transform, "[TitleText]", "NEKO GALLERY", 32, FontStyle.Bold, TextAnchor.MiddleLeft); RectTransform tr = title.rectTransform; tr.anchorMin = new Vector2(0f,0f); tr.anchorMax = new Vector2(.65f,.18f); tr.offsetMin = new Vector2(30f,64f); tr.offsetMax = new Vector2(0f,-8f);
            Text subtitle = TextNode(frame.transform, "[SubtitleText]", "Local / JSON / remote image demo", 20, FontStyle.Normal, TextAnchor.MiddleLeft); RectTransform sr = subtitle.rectTransform; sr.anchorMin = new Vector2(0f,0f); sr.anchorMax = new Vector2(.7f,.18f); sr.offsetMin = new Vector2(30f,18f); sr.offsetMax = new Vector2(0f,-46f);
            Text page = TextNode(frame.transform, "[PageText]", "1 / 3", 20, FontStyle.Bold, TextAnchor.MiddleCenter); RectTransform pr = page.rectTransform; pr.anchorMin = new Vector2(.75f,0f); pr.anchorMax = new Vector2(.9f,.13f); pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
            Text status = TextNode(frame.transform, "[StatusText]", "", 15, FontStyle.Normal, TextAnchor.MiddleLeft); RectTransform str = status.rectTransform; str.anchorMin = new Vector2(0f,0f); str.anchorMax = new Vector2(.55f,.08f); str.offsetMin = new Vector2(30f,0f); str.offsetMax = Vector2.zero;

            ButtonNode(frame.transform, "[PrevButton]", "‹", new Vector2(.68f,.065f), new Vector2(70f,52f));
            ButtonNode(frame.transform, "[NextButton]", "›", new Vector2(.94f,.065f), new Vector2(70f,52f));
            ButtonNode(frame.transform, "[AutoButton]", "AUTO", new Vector2(.81f,.065f), new Vector2(105f,52f));

            CopyRuntime();
            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("Image Gallery", "Created the demo gallery and refreshed the runtime script. Wait for Unity/UdonSharp to compile, then click AUTO-WIRE / REPAIR SELECTED GALLERY.", "OK");
        }

        void AutoWire(GameObject root)
        {
            if (root == null || !root.name.Contains("Gallery")) { EditorUtility.DisplayDialog("Image Gallery", "Select the generated Neko Image Gallery root.", "OK"); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorUtility.DisplayDialog("Image Gallery", "Unity is still compiling/importing. Wait for it to finish and try again.", "OK"); return; }

            Type type = FindType("NekoImageGalleryRuntime");
            if (type == null) { EditorUtility.DisplayDialog("Image Gallery", "NekoImageGalleryRuntime has not compiled yet. Use REPAIR / COPY LATEST RUNTIME, fix any remaining Console errors, then try again.", "OK"); return; }

            bool createdProgram;
            if (!EnsureUdonSharpProgramAsset(type, out createdProgram)) { EditorUtility.DisplayDialog("Image Gallery", "Could not find/create the UdonSharpProgramAsset. Run VRChat SDK → Udon Sharp → Refresh All UdonSharp Assets, then try again.", "OK"); return; }
            if (createdProgram || EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorUtility.DisplayDialog("Image Gallery", "Created/repaired NekoImageGalleryRuntime.asset. Let UdonSharp finish compiling, then Auto-Wire again.", "OK"); return; }

            Component c = root.GetComponent(type); if (c == null) c = AddUdonSharpComponent(root, type); else RunUdonSharpSetup(c);
            if (c == null) { EditorUtility.DisplayDialog("Image Gallery", "UdonSharp could not attach the gallery runtime through its editor API. Check the Console and try again.", "OK"); return; }

            GameObject frontGo=Find(root,"[FrontImage]"), backGo=Find(root,"[BackImage]"), fxGo=Find(root,"[ShaderImage]");
            if(frontGo==null||backGo==null||fxGo==null){EditorUtility.DisplayDialog("Image Gallery","Required generated image layers are missing.","OK");return;}
            RawImage front = frontGo.GetComponent<RawImage>(); RawImage back = backGo.GetComponent<RawImage>(); RawImage fx = fxGo.GetComponent<RawImage>();
            Set(c,"frontImage",front); Set(c,"backImage",back); Set(c,"shaderImage",fx); Set(c,"frontGroup",front.GetComponent<CanvasGroup>()); Set(c,"backGroup",back.GetComponent<CanvasGroup>());
            Set(c,"titleText",Find(root,"[TitleText]").GetComponent<Text>()); Set(c,"subtitleText",Find(root,"[SubtitleText]").GetComponent<Text>()); Set(c,"pageText",Find(root,"[PageText]").GetComponent<Text>()); Set(c,"statusText",Find(root,"[StatusText]").GetComponent<Text>());
            Set(c,"transitionMode",_effect); Set(c,"transitionDuration",_duration); Set(c,"autoPlay",_autoPlay); Set(c,"autoPlaySeconds",_autoSeconds); Set(c,"sourceMode",_sourceMode);
            Set(c,"rootKey",_rootPath); Set(c,"titleKey",_titleKey); Set(c,"subtitleKey",_subtitleKey); Set(c,"imageIndexKey",_indexKey); Set(c,"imageUrlKey",_urlKey);

            Texture[] textures = new Texture[] { AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedFolder+"/Demo_A.asset"), AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedFolder+"/Demo_B.asset"), AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedFolder+"/Demo_C.asset") };
            Set(c,"localTextures",textures); Set(c,"transitionMaterial",AssetDatabase.LoadAssetAtPath<Material>(GeneratedFolder+"/NekoGalleryTransition.mat"));

            ApplyRemoteConfiguration(c);

            Wire(Find(root,"[PrevButton]").GetComponent<Button>(), c, "Previous"); Wire(Find(root,"[NextButton]").GetComponent<Button>(), c, "Next"); Wire(Find(root,"[AutoButton]").GetComponent<Button>(), c, "ToggleAutoPlay");
            ApplyUdonSharpProxy(c); EditorUtility.SetDirty(c);
            EditorUtility.DisplayDialog("Image Gallery", "Gallery auto-wired/repaired. Remote JSON supports root/dotted array paths plus full URL, relative-path and alias mapping onto predeclared VRCUrl entries.", "OK");
        }

        void ApplyRemoteConfiguration(Component c)
        {
            Type urlType=FindType("VRC.SDKBase.VRCUrl"); if(urlType==null)return;
            if(!string.IsNullOrWhiteSpace(_remoteJsonUrl)) Set(c,"metadataJsonUrl",CreateVrcUrl(urlType,_remoteJsonUrl.Trim()));

            string[] urls=Lines(_predeclaredUrls); string[] aliases=Lines(_urlAliases);
            if(urls.Length>0)
            {
                Array array=Array.CreateInstance(urlType,urls.Length);
                for(int i=0;i<urls.Length;i++)array.SetValue(CreateVrcUrl(urlType,urls[i]),i);
                Set(c,"imageUrls",array);
            }
            if(aliases.Length>0)Set(c,"imageUrlMapKeys",aliases);
        }

        static object CreateVrcUrl(Type urlType,string value)
        {
            try{return Activator.CreateInstance(urlType,new object[]{value});}catch{return null;}
        }

        static string[] Lines(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return new string[0]; string[] raw=value.Replace("\r","").Split('\n'); var list=new List<string>(); for(int i=0;i<raw.Length;i++){string s=raw[i].Trim();if(!string.IsNullOrEmpty(s))list.Add(s);}return list.ToArray();
        }

        Texture2D[] CreateDemoTextures(string folder)
        {
            return new[] { MakeDemo(folder+"/Demo_A.asset", new Color(.12f,.38f,.72f), new Color(.52f,.16f,.72f)), MakeDemo(folder+"/Demo_B.asset", new Color(.08f,.62f,.48f), new Color(.08f,.24f,.46f)), MakeDemo(folder+"/Demo_C.asset", new Color(.9f,.35f,.18f), new Color(.34f,.08f,.34f)) };
        }

        Texture2D MakeDemo(string path, Color a, Color b)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            var t = new Texture2D(320,180,TextureFormat.RGBA32,false); t.name = Path.GetFileNameWithoutExtension(path);
            for(int y=0;y<t.height;y++) for(int x=0;x<t.width;x++) { float u=(float)x/(t.width-1); float v=(float)y/(t.height-1); Color c=Color.Lerp(a,b,(u+v)*.5f); float glow=Mathf.Clamp01(1f-Vector2.Distance(new Vector2(u,v),new Vector2(.5f,.5f))*1.8f); c+=Color.white*glow*.18f; c.a=1f; t.SetPixel(x,y,c); }
            t.Apply(); AssetDatabase.CreateAsset(t,path); return t;
        }

        Material CreateTransitionMaterial(string folder)
        {
            string path = folder + "/NekoGalleryTransition.mat"; Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            Shader shader = Shader.Find("NekoSune/WorldGalleryTransition"); if (shader == null) return null;
            Material m = new Material(shader); AssetDatabase.CreateAsset(m,path); return m;
        }

        static bool EnsureUdonSharpProgramAsset(Type runtimeType,out bool created)
        {
            created=false; Type programType=FindType("UdonSharp.UdonSharpProgramAsset"); if(programType==null)return false;
            MethodInfo getProgram=programType.GetMethod("GetProgramAssetForClass",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(Type)},null);
            if(getProgram!=null){try{if(getProgram.Invoke(null,new object[]{runtimeType})!=null)return true;}catch{}}
            MonoScript source=AssetDatabase.LoadAssetAtPath<MonoScript>(GeneratedScriptPath); if(source==null)return false;
            UnityEngine.Object existing=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GeneratedProgramPath); FieldInfo sourceField=programType.GetField("sourceCsScript",BindingFlags.Public|BindingFlags.Instance); if(sourceField==null)return false;
            if(existing==null){ScriptableObject program=ScriptableObject.CreateInstance(programType);sourceField.SetValue(program,source);AssetDatabase.CreateAsset(program,GeneratedProgramPath);EditorUtility.SetDirty(program);AssetDatabase.SaveAssets();created=true;}
            else if(sourceField.GetValue(existing)==null){sourceField.SetValue(existing,source);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssets();created=true;}
            TryCompileUdonSharp(programType);AssetDatabase.ImportAsset(GeneratedProgramPath,ImportAssetOptions.ForceUpdate);return true;
        }

        static void TryCompileUdonSharp(Type programType)
        {
            try{MethodInfo compile=programType.GetMethod("CompileAllCsPrograms",BindingFlags.Public|BindingFlags.Static);if(compile==null)return;ParameterInfo[]p=compile.GetParameters();if(p.Length==2)compile.Invoke(null,new object[]{true,true});else if(p.Length==1)compile.Invoke(null,new object[]{true});else compile.Invoke(null,null);}catch(Exception e){Debug.LogWarning("[NekoSune Gallery] UdonSharp compile request deferred: "+e.Message);}
        }

        static Component AddUdonSharpComponent(GameObject root,Type runtimeType)
        {
            Type ext=FindType("UdonSharpEditor.UdonSharpComponentExtensions");if(ext==null)return null;MethodInfo add=ext.GetMethod("AddUdonSharpComponent",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(GameObject),typeof(Type)},null);if(add==null)return null;try{return add.Invoke(null,new object[]{root,runtimeType}) as Component;}catch(Exception e){string msg=e.InnerException!=null?e.InnerException.Message:e.Message;Debug.LogError("[NekoSune Gallery] UdonSharp AddComponent failed: "+msg);return null;}
        }
        static void RunUdonSharpSetup(Component component){InvokeUdonSharpUtility("RunBehaviourSetup",component);}
        static void ApplyUdonSharpProxy(Component component){InvokeUdonSharpUtility("CopyProxyToUdon",component);}
        static void InvokeUdonSharpUtility(string methodName,Component component){Type utility=FindType("UdonSharpEditor.UdonSharpEditorUtility");if(utility==null||component==null)return;MethodInfo[]ms=utility.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);for(int i=0;i<ms.Length;i++){MethodInfo m=ms[i];if(m.Name!=methodName)continue;ParameterInfo[]p=m.GetParameters();if(p.Length==1&&p[0].ParameterType.IsAssignableFrom(component.GetType())){try{m.Invoke(null,new object[]{component});}catch{}return;}}}

        static GameObject Ui(string name, Transform parent, params Type[] components) { Type[] all = new Type[components.Length + 1]; all[0] = typeof(RectTransform); Array.Copy(components,0,all,1,components.Length); GameObject go = new GameObject(name, all); go.transform.SetParent(parent,false); return go; }
        static RawImage Raw(Transform parent,string name) { return Ui(name,parent,typeof(RawImage)).GetComponent<RawImage>(); }
        static Text TextNode(Transform parent,string name,string value,int size,FontStyle style,TextAnchor align) { Text t=Ui(name,parent,typeof(Text)).GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text=value; t.fontSize=size; t.fontStyle=style; t.alignment=align; t.color=Color.white; return t; }
        static Button ButtonNode(Transform parent,string name,string value,Vector2 anchor,Vector2 size) { GameObject go=Ui(name,parent,typeof(Image),typeof(Button)); RectTransform r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anchor; r.pivot=new Vector2(.5f,.5f); r.sizeDelta=size; go.GetComponent<Image>().color=new Color(.29f,.56f,.98f,1f); Text t=TextNode(go.transform,"Label",value,28,FontStyle.Bold,TextAnchor.MiddleCenter); Stretch(t.rectTransform,4f); return go.GetComponent<Button>(); }
        static void Stretch(RectTransform r,float p){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(p,p);r.offsetMax=new Vector2(-p,-p);}
        static GameObject Find(GameObject root,string name){Transform[] all=root.GetComponentsInChildren<Transform>(true);for(int i=0;i<all.Length;i++)if(all[i].name==name)return all[i].gameObject;return null;}
        static Type FindType(string simple){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){Type direct=a.GetType(simple,false);if(direct!=null)return direct;Type[] ts;try{ts=a.GetTypes();}catch{continue;}for(int i=0;i<ts.Length;i++)if(ts[i]!=null&&(ts[i].Name==simple||ts[i].FullName==simple))return ts[i];}return null;}
        static void Set(Component c,string field,object value){FieldInfo f=c.GetType().GetField(field,BindingFlags.Public|BindingFlags.Instance);if(f!=null){Undo.RecordObject(c,"Wire gallery");try{f.SetValue(c,value);}catch(Exception e){Debug.LogWarning("[NekoSune Gallery] Could not assign "+field+": "+e.Message);}EditorUtility.SetDirty(c);}}
        static void Wire(Button b,Component c,string method){if(b==null||c==null)return;MethodInfo m=c.GetType().GetMethod(method,BindingFlags.Public|BindingFlags.Instance);if(m==null)return;try{UnityAction a=(UnityAction)Delegate.CreateDelegate(typeof(UnityAction),c,m);UnityEventTools.AddPersistentListener(b.onClick,a);EditorUtility.SetDirty(b);}catch(Exception e){Debug.LogWarning("[NekoSune Gallery] Could not wire "+method+": "+e.Message);}}
        static void TryAddVrcUiShape(GameObject root){Type t=FindType("VRC.SDK3.Components.VRCUiShape");if(t==null)t=FindType("VRCUiShape");if(t==null)t=FindType("VRC_UiShape");if(t!=null&&root.GetComponent(t)==null)Undo.AddComponent(root,t);}

        static void CopyRuntime(){CopyPackageFile("Templates/Runtime/NekoImageGalleryRuntime.cs.txt",GeneratedScriptPath);}
        static void CopyExampleJson(){string dst="Assets/NekoSune/Gallery/Examples/gallery-items.json";EnsureFolder("Assets/NekoSune/Gallery/Examples");CopyPackageFile("Examples/gallery-items.json",dst);EditorUtility.DisplayDialog("Image Gallery","Copied "+dst,"OK");}
        static void CopyPackageFile(string sourceRelative,string target){string src=Path.GetFullPath(Path.Combine("Packages/com.nekosune.world-gallery",sourceRelative));if(!File.Exists(src)){Debug.LogError("Missing package file: "+src);return;}EnsureFolder(Path.GetDirectoryName(target).Replace('\\','/'));string abs=Path.Combine(Directory.GetParent(Application.dataPath).FullName,target.Replace('/',Path.DirectorySeparatorChar));File.Copy(src,abs,true);AssetDatabase.Refresh();}
        static string EnsureFolder(string path){string[] p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}return cur;}
    }
}

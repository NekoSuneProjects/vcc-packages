using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NekoSune.WorldAvatarSearch.Editor
{
    internal sealed class NekoAvatarSearchBuilderWindow : EditorWindow
    {
        const string DemoUrl = "https://vrcavatarsearch.nekosunevr.co.uk/vrcx_search?search=Rindo";
        const string GeneratedFolder = "Assets/NekoSune/AvatarSearch/Generated";
        const string GeneratedScriptPath = GeneratedFolder + "/NekoAvatarSearchBrowser.cs";
        const string GeneratedProgramPath = GeneratedFolder + "/NekoAvatarSearchBrowser.asset";

        Vector2 _scroll;
        int _pageSize = 10;
        string _rootKey = "";
        string _idKey = "id";
        string _nameKey = "name";
        string _authorKey = "authorName";
        string _descriptionKey = "description";
        string _statusKey = "releaseStatus";
        string _thumbnailKey = "thumbnailImageUrl";

        [MenuItem("NekoSune/World/Avatar Search Builder", false, 25)]
        public static void Open()
        {
            var w = GetWindow<NekoAvatarSearchBuilderWindow>(false, "Avatar Search Builder", true);
            w.minSize = new Vector2(790f, 640f);
            w.Show();
        }

        void OnEnable() { UpdateChecker.Changed += Repaint; }
        void OnDisable() { UpdateChecker.Changed -= Repaint; }

        void OnGUI()
        {
            NekoStyles.Ensure();
            EditorGUILayout.BeginHorizontal();
            NekoStyles.HeaderBar("Avatar Search", "NekoSune", "Beginner-friendly paged avatar browser for VRCX-style and other JSON APIs");
            NekoUpdateChrome.DrawVersionChip();
            EditorGUILayout.EndHorizontal();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            NekoUpdateChrome.DrawUpdateBanner();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Demo layout", NekoStyles.SlotName);
            _pageSize = EditorGUILayout.IntPopup("Results per page", _pageSize, new[] { "5", "10" }, new[] { 5, 10 });
            EditorGUILayout.LabelField("The generated UI always owns 10 reusable result slots. 5/page hides the extra five; 10/page uses all of them.", NekoStyles.WrapLabel);
            if (GUILayout.Button("BUILD AVATAR SEARCH DEMO", NekoStyles.PrimaryButton, GUILayout.Height(36f))) BuildDemo();
            if (GUILayout.Button("AUTO-WIRE / REPAIR SELECTED SEARCH UI", NekoStyles.PrimaryButton, GUILayout.Height(34f))) AutoWire(Selection.activeGameObject);
            EditorGUILayout.HelpBox("Auto-Wire now verifies the generated UdonSharpProgramAsset before attaching the behaviour. If it has to create/repair that asset, let Unity finish compiling and click Auto-Wire again.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Flexible JSON mapper", NekoStyles.SlotName);
            _rootKey = EditorGUILayout.TextField("Root array key", _rootKey);
            _idKey = EditorGUILayout.TextField("Avatar ID", _idKey);
            _nameKey = EditorGUILayout.TextField("Avatar name", _nameKey);
            _authorKey = EditorGUILayout.TextField("Author", _authorKey);
            _descriptionKey = EditorGUILayout.TextField("Description", _descriptionKey);
            _statusKey = EditorGUILayout.TextField("Release status", _statusKey);
            _thumbnailKey = EditorGUILayout.TextField("Thumbnail URL metadata", _thumbnailKey);
            EditorGUILayout.HelpBox("Blank Root Key means a root JSON array. The runtime also checks avatars/results/items/data wrappers and common field aliases.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("VRChat search URL behaviour", NekoStyles.SlotName);
            EditorGUILayout.LabelField("VRChat Udon cannot freely construct arbitrary VRCUrl values from normal strings. The demo therefore predeclares the Rindo search and provides a real VRCUrlInputField for complete user-entered API URLs.", NekoStyles.WrapLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Avatar interaction", NekoStyles.SlotName);
            EditorGUILayout.LabelField("PREVIEW changes a VRCAvatarPedestal. USE AVATAR then calls SetAvatarUse for the local player. VRChat still enforces normal avatar visibility/ownership rules.", NekoStyles.WrapLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        void BuildDemo()
        {
            Type urlInputType = FindType("VRC.SDK3.Components.VRCUrlInputField");
            Type pedestalType = FindType("VRC.SDK3.Components.VRCAvatarPedestal");
            if (pedestalType == null) pedestalType = FindType("VRC.SDKBase.VRC_AvatarPedestal");
            if (urlInputType == null || pedestalType == null)
            {
                EditorUtility.DisplayDialog("Avatar Search", "VRChat Worlds SDK components were not found.", "OK");
                return;
            }

            GameObject root = Ui("Neko Avatar Search UI", null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            Undo.RegisterCreatedObjectUndo(root, "Create avatar search UI");
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1400f, 940f);
            root.transform.localScale = Vector3.one * .001f;
            root.GetComponent<Image>().color = new Color(.025f,.032f,.055f,.985f);
            TryAddVrcUiShape(root);

            Text header = TextNode(root.transform, "Header", "AVATAR SEARCH", 44, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(header.rectTransform, new Vector2(.03f,.91f), new Vector2(.50f,.985f));
            Text sub = TextNode(root.transform, "SubHeader", "JSON / VRCX browser • 5 or 10 results per page", 19, FontStyle.Normal, TextAnchor.MiddleLeft);
            sub.color = new Color(.60f,.68f,.80f);
            SetRect(sub.rectTransform, new Vector2(.03f,.865f), new Vector2(.62f,.915f));

            GameObject urlBox = UiDynamic("[UrlInput]", root.transform, new[] { typeof(Image), urlInputType });
            SetRect(urlBox.GetComponent<RectTransform>(), new Vector2(.03f,.795f), new Vector2(.66f,.85f));
            Image bg = urlBox.GetComponent<Image>(); bg.color = new Color(.08f,.10f,.15f,1f);
            Component vrcInput = urlBox.GetComponent(urlInputType);
            Text inputText = TextNode(urlBox.transform, "Text", "", 18, FontStyle.Normal, TextAnchor.MiddleLeft); Stretch(inputText.rectTransform, 12f);
            Text placeholder = TextNode(urlBox.transform, "Placeholder", "Paste complete API URL...", 17, FontStyle.Italic, TextAnchor.MiddleLeft); placeholder.color = new Color(.52f,.56f,.66f); Stretch(placeholder.rectTransform,12f);
            SetMember(vrcInput, "textComponent", inputText);
            SetMember(vrcInput, "placeholder", placeholder);
            SetMember(vrcInput, "targetGraphic", bg);
            SetMember(vrcInput, "navigation", new Navigation { mode = Navigation.Mode.None });
            SetStringMember(vrcInput, "text", DemoUrl);

            ButtonNode(root.transform,"[SearchButton]","SEARCH",new Vector2(.675f,.795f),new Vector2(.79f,.85f));
            ButtonNode(root.transform,"[DemoButton]","DEMO RINDO",new Vector2(.80f,.795f),new Vector2(.97f,.85f));

            GameObject listPanel = Ui("Results", root.transform, typeof(Image));
            SetRect(listPanel.GetComponent<RectTransform>(), new Vector2(.03f,.15f), new Vector2(.57f,.765f));
            listPanel.GetComponent<Image>().color = new Color(.045f,.055f,.085f,1f);
            var layout = listPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12,12,12,12); layout.spacing = 5f; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            for (int i = 0; i < 10; i++) CreateResultCard(listPanel.transform, i);

            GameObject detail = Ui("Details", root.transform, typeof(Image));
            SetRect(detail.GetComponent<RectTransform>(), new Vector2(.59f,.15f), new Vector2(.97f,.765f));
            detail.GetComponent<Image>().color = new Color(.055f,.067f,.105f,1f);
            Text selectedName = TextNode(detail.transform,"[SelectedName]","NO AVATAR SELECTED",32,FontStyle.Bold,TextAnchor.UpperLeft); SetRect(selectedName.rectTransform,new Vector2(.06f,.78f),new Vector2(.94f,.94f));
            Text selectedAuthor = TextNode(detail.transform,"[SelectedAuthor]","",20,FontStyle.Bold,TextAnchor.UpperLeft); selectedAuthor.color=new Color(.4f,.68f,1f); SetRect(selectedAuthor.rectTransform,new Vector2(.06f,.69f),new Vector2(.94f,.80f));
            Text selectedDesc = TextNode(detail.transform,"[SelectedDescription]","Search for an avatar to begin.",18,FontStyle.Normal,TextAnchor.UpperLeft); selectedDesc.horizontalOverflow=HorizontalWrapMode.Wrap; selectedDesc.verticalOverflow=VerticalWrapMode.Truncate; SetRect(selectedDesc.rectTransform,new Vector2(.06f,.36f),new Vector2(.94f,.69f));
            Text selectedId = TextNode(detail.transform,"[SelectedId]","",13,FontStyle.Normal,TextAnchor.UpperLeft); selectedId.color=new Color(.62f,.65f,.72f); SetRect(selectedId.rectTransform,new Vector2(.06f,.28f),new Vector2(.94f,.36f));
            ButtonNode(detail.transform,"[PreviewButton]","PREVIEW",new Vector2(.06f,.14f),new Vector2(.46f,.24f));
            ButtonNode(detail.transform,"[UseButton]","USE AVATAR",new Vector2(.50f,.14f),new Vector2(.94f,.24f));
            Text note = TextNode(detail.transform,"Note","3D pedestal preview • switching affects only your local player",14,FontStyle.Normal,TextAnchor.UpperLeft); note.color=new Color(.65f,.68f,.75f); SetRect(note.rectTransform,new Vector2(.06f,.04f),new Vector2(.94f,.12f));

            ButtonNode(root.transform,"[PreviousPageButton]","‹ PAGE",new Vector2(.03f,.085f),new Vector2(.14f,.135f));
            Text page = TextNode(root.transform,"[PageText]","Page 0 / 0",17,FontStyle.Bold,TextAnchor.MiddleCenter); SetRect(page.rectTransform,new Vector2(.15f,.085f),new Vector2(.36f,.135f));
            ButtonNode(root.transform,"[NextPageButton]","PAGE ›",new Vector2(.37f,.085f),new Vector2(.48f,.135f));
            ButtonNode(root.transform,"[Page5Button]","5 / PAGE",new Vector2(.50f,.085f),new Vector2(.60f,.135f));
            Text pageSize = TextNode(root.transform,"[PageSizeText]",_pageSize+" / PAGE",15,FontStyle.Bold,TextAnchor.MiddleCenter); pageSize.color=new Color(.54f,.75f,1f); SetRect(pageSize.rectTransform,new Vector2(.61f,.085f),new Vector2(.72f,.135f));
            ButtonNode(root.transform,"[Page10Button]","10 / PAGE",new Vector2(.73f,.085f),new Vector2(.84f,.135f));

            Text status = TextNode(root.transform,"[StatusText]","Edit the Rindo URL or press DEMO RINDO.",16,FontStyle.Normal,TextAnchor.MiddleLeft); status.color=new Color(.64f,.70f,.82f); SetRect(status.rectTransform,new Vector2(.03f,.015f),new Vector2(.97f,.07f));

            GameObject pedestalGo = new GameObject("Neko Avatar Search Preview Pedestal", pedestalType);
            Undo.RegisterCreatedObjectUndo(pedestalGo,"Create avatar preview pedestal");
            pedestalGo.transform.position = root.transform.position + root.transform.right * 1.8f;
            Component pedestal = pedestalGo.GetComponent(pedestalType);
            SetMember(pedestal,"ChangeAvatarsOnUse",true);
            SetMember(pedestal,"scale",.85f);
            GameObject placement = new GameObject("Preview Placement"); placement.transform.SetParent(pedestalGo.transform,false); SetMember(pedestal,"Placement",placement.transform);

            CopyRuntime();
            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("Avatar Search", "Created 10 reusable result slots and refreshed NekoAvatarSearchBrowser.cs. Let Unity compile, then click AUTO-WIRE / REPAIR. The first repair may create the missing UdonSharpProgramAsset and ask you to wait once more.", "OK");
        }

        void CreateResultCard(Transform parent, int index)
        {
            GameObject card = Ui("[Result"+index+"]", parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            card.GetComponent<Image>().color = index % 2 == 0 ? new Color(.075f,.09f,.135f,1f) : new Color(.065f,.078f,.118f,1f);
            card.GetComponent<LayoutElement>().preferredHeight = 50f;
            Text name = TextNode(card.transform,"[Result"+index+"Name]","Avatar Result",18,FontStyle.Bold,TextAnchor.UpperLeft); SetRect(name.rectTransform,new Vector2(.025f,.42f),new Vector2(.73f,.95f));
            Text author = TextNode(card.transform,"[Result"+index+"Author]","by creator",13,FontStyle.Normal,TextAnchor.LowerLeft); author.color=new Color(.63f,.68f,.78f); SetRect(author.rectTransform,new Vector2(.025f,.04f),new Vector2(.73f,.46f));
            Text desc = TextNode(card.transform,"[Result"+index+"Description]","",11,FontStyle.Normal,TextAnchor.MiddleLeft); desc.gameObject.SetActive(false);
            Text status = TextNode(card.transform,"[Result"+index+"Status]","PUBLIC",12,FontStyle.Bold,TextAnchor.MiddleCenter); status.color=new Color(.45f,.86f,.58f); SetRect(status.rectTransform,new Vector2(.75f,.18f),new Vector2(.975f,.82f));
            card.SetActive(false);
        }

        void AutoWire(GameObject root)
        {
            if (root == null || root.name != "Neko Avatar Search UI") { EditorUtility.DisplayDialog("Avatar Search","Select the generated Neko Avatar Search UI root.","OK"); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorUtility.DisplayDialog("Avatar Search","Unity is still compiling/importing. Wait for it to finish, then Auto-Wire again.","OK"); return; }

            Type runtimeType = FindType("NekoAvatarSearchBrowser");
            Type urlInputType = FindType("VRC.SDK3.Components.VRCUrlInputField");
            Type pedestalType = FindType("VRC.SDK3.Components.VRCAvatarPedestal"); if (pedestalType == null) pedestalType = FindType("VRC.SDKBase.VRC_AvatarPedestal");
            Type urlType = FindType("VRC.SDKBase.VRCUrl");
            if (runtimeType == null) { EditorUtility.DisplayDialog("Avatar Search","NekoAvatarSearchBrowser has not compiled yet. Check the Console for compile errors, then try again.","OK"); return; }

            bool createdProgram;
            if (!EnsureUdonSharpProgramAsset(runtimeType, out createdProgram))
            {
                EditorUtility.DisplayDialog("Avatar Search","Could not find/create the UdonSharpProgramAsset for NekoAvatarSearchBrowser. Use VRChat SDK → Udon Sharp → Refresh All UdonSharp Assets, then try Auto-Wire again.","OK");
                return;
            }
            if (createdProgram || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("Avatar Search","Created/repaired NekoAvatarSearchBrowser.asset. Let UdonSharp finish compiling it, then click Auto-Wire again. No behaviour was attached yet, so this avoids the invalid-program-asset error.","OK");
                return;
            }

            Component c = root.GetComponent(runtimeType);
            if (c == null) c = AddUdonSharpComponent(root, runtimeType);
            else RunUdonSharpSetup(c);
            if (c == null) { EditorUtility.DisplayDialog("Avatar Search","UdonSharp could not attach NekoAvatarSearchBrowser through its editor API. Check the Console and run Auto-Wire again after UdonSharp finishes compiling.","OK"); return; }

            GameObject urlInputGo = Find(root,"[UrlInput]");
            if (urlInputGo == null || urlInputType == null) { EditorUtility.DisplayDialog("Avatar Search","Generated VRCUrlInputField is missing.","OK"); return; }
            Set(c,"searchUrlInput",urlInputGo.GetComponent(urlInputType));
            if (urlType != null) Set(c,"demoSearchUrl",Activator.CreateInstance(urlType,new object[]{DemoUrl}));
            Set(c,"resultsPerPage",_pageSize);
            Set(c,"rootKey",_rootKey); Set(c,"idKey",_idKey); Set(c,"nameKey",_nameKey); Set(c,"authorKey",_authorKey); Set(c,"descriptionKey",_descriptionKey); Set(c,"releaseStatusKey",_statusKey); Set(c,"thumbnailKey",_thumbnailKey);

            GameObject[] cards = new GameObject[10]; Text[] names = new Text[10]; Text[] authors = new Text[10]; Text[] descs = new Text[10]; Text[] statuses = new Text[10];
            for (int i = 0; i < 10; i++)
            {
                cards[i]=Find(root,"[Result"+i+"]"); names[i]=Find(root,"[Result"+i+"Name]").GetComponent<Text>(); authors[i]=Find(root,"[Result"+i+"Author]").GetComponent<Text>(); descs[i]=Find(root,"[Result"+i+"Description]").GetComponent<Text>(); statuses[i]=Find(root,"[Result"+i+"Status]").GetComponent<Text>();
                Wire(cards[i].GetComponent<Button>(),c,"Select"+i);
            }
            Set(c,"resultCards",cards); Set(c,"resultNames",names); Set(c,"resultAuthors",authors); Set(c,"resultDescriptions",descs); Set(c,"resultStatus",statuses);
            Set(c,"resultsPageText",Find(root,"[PageText]").GetComponent<Text>()); Set(c,"pageSizeText",Find(root,"[PageSizeText]").GetComponent<Text>());
            Set(c,"selectedName",Find(root,"[SelectedName]").GetComponent<Text>()); Set(c,"selectedAuthor",Find(root,"[SelectedAuthor]").GetComponent<Text>()); Set(c,"selectedDescription",Find(root,"[SelectedDescription]").GetComponent<Text>()); Set(c,"selectedId",Find(root,"[SelectedId]").GetComponent<Text>()); Set(c,"statusText",Find(root,"[StatusText]").GetComponent<Text>());

            GameObject p=GameObject.Find("Neko Avatar Search Preview Pedestal"); if(p!=null&&pedestalType!=null) Set(c,"previewPedestal",p.GetComponent(pedestalType));
            Wire(Find(root,"[SearchButton]").GetComponent<Button>(),c,"SearchFromUrlField"); Wire(Find(root,"[DemoButton]").GetComponent<Button>(),c,"SearchDemo"); Wire(Find(root,"[PreviewButton]").GetComponent<Button>(),c,"PreviewSelected"); Wire(Find(root,"[UseButton]").GetComponent<Button>(),c,"UseSelectedAvatar");
            Wire(Find(root,"[PreviousPageButton]").GetComponent<Button>(),c,"PreviousPage"); Wire(Find(root,"[NextPageButton]").GetComponent<Button>(),c,"NextPage"); Wire(Find(root,"[Page5Button]").GetComponent<Button>(),c,"SetPageSize5"); Wire(Find(root,"[Page10Button]").GetComponent<Button>(),c,"SetPageSize10");
            ApplyUdonSharpProxy(c);
            EditorUtility.SetDirty(c);
            EditorUtility.DisplayDialog("Avatar Search", "Auto-wired with "+_pageSize+" results per page and a verified UdonSharp program asset.", "OK");
        }

        static bool EnsureUdonSharpProgramAsset(Type runtimeType, out bool created)
        {
            created = false;
            Type programType = FindType("UdonSharp.UdonSharpProgramAsset");
            if (programType == null) return false;

            MethodInfo getProgram = programType.GetMethod("GetProgramAssetForClass", BindingFlags.Public|BindingFlags.Static, null, new[]{typeof(Type)}, null);
            if (getProgram != null)
            {
                try { if (getProgram.Invoke(null,new object[]{runtimeType}) != null) return true; } catch {}
            }

            MonoScript source = AssetDatabase.LoadAssetAtPath<MonoScript>(GeneratedScriptPath);
            if (source == null) return false;
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GeneratedProgramPath);
            if (existing == null)
            {
                ScriptableObject program = ScriptableObject.CreateInstance(programType);
                FieldInfo sourceField = programType.GetField("sourceCsScript",BindingFlags.Public|BindingFlags.Instance);
                if (sourceField == null) { UnityEngine.Object.DestroyImmediate(program); return false; }
                sourceField.SetValue(program,source);
                AssetDatabase.CreateAsset(program,GeneratedProgramPath);
                EditorUtility.SetDirty(program);
                AssetDatabase.SaveAssets();
                created = true;
            }
            else
            {
                FieldInfo sourceField = programType.GetField("sourceCsScript",BindingFlags.Public|BindingFlags.Instance);
                if (sourceField != null && sourceField.GetValue(existing) == null) { sourceField.SetValue(existing,source); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssets(); created = true; }
            }

            TryCompileUdonSharp(programType);
            AssetDatabase.ImportAsset(GeneratedProgramPath,ImportAssetOptions.ForceUpdate);
            if (created) return true;
            if (getProgram == null) return existing != null;
            try { return getProgram.Invoke(null,new object[]{runtimeType}) != null; } catch { return false; }
        }

        static void TryCompileUdonSharp(Type programType)
        {
            try
            {
                MethodInfo compile = programType.GetMethod("CompileAllCsPrograms",BindingFlags.Public|BindingFlags.Static);
                if (compile == null) return;
                ParameterInfo[] ps = compile.GetParameters();
                if (ps.Length == 2) compile.Invoke(null,new object[]{true,true});
                else if (ps.Length == 1) compile.Invoke(null,new object[]{true});
                else compile.Invoke(null,null);
            }
            catch (Exception e) { Debug.LogWarning("[NekoSune Avatar Search] UdonSharp compile request deferred: "+e.Message); }
        }

        static Component AddUdonSharpComponent(GameObject root, Type runtimeType)
        {
            Type extensions = FindType("UdonSharpEditor.UdonSharpComponentExtensions");
            if (extensions != null)
            {
                MethodInfo add = extensions.GetMethod("AddUdonSharpComponent",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(GameObject),typeof(Type)},null);
                if (add != null)
                {
                    try { return add.Invoke(null,new object[]{root,runtimeType}) as Component; }
                    catch (Exception e) { Debug.LogError("[NekoSune Avatar Search] UdonSharp AddComponent failed: "+(e.InnerException != null ? e.InnerException.Message : e.Message)); return null; }
                }
            }
            Debug.LogError("[NekoSune Avatar Search] UdonSharp editor AddUdonSharpComponent API was not found.");
            return null;
        }

        static void RunUdonSharpSetup(Component component)
        {
            Type utility = FindType("UdonSharpEditor.UdonSharpEditorUtility");
            if (utility == null || component == null) return;
            MethodInfo[] methods = utility.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.NonPublic);
            for (int i=0;i<methods.Length;i++)
            {
                MethodInfo m=methods[i]; if(m.Name!="RunBehaviourSetup")continue; ParameterInfo[] p=m.GetParameters();
                if(p.Length==1&&p[0].ParameterType.IsAssignableFrom(component.GetType())){try{m.Invoke(null,new object[]{component});}catch{}return;}
            }
        }

        static void ApplyUdonSharpProxy(Component component)
        {
            Type utility = FindType("UdonSharpEditor.UdonSharpEditorUtility");
            if (utility == null || component == null) return;
            MethodInfo[] methods = utility.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.NonPublic);
            for (int i=0;i<methods.Length;i++)
            {
                MethodInfo m=methods[i]; if(m.Name!="CopyProxyToUdon")continue; ParameterInfo[] p=m.GetParameters();
                if(p.Length==1&&p[0].ParameterType.IsAssignableFrom(component.GetType())){try{m.Invoke(null,new object[]{component});}catch{}return;}
            }
        }

        static GameObject Ui(string name, Transform parent, params Type[] components) { return UiDynamic(name,parent,components); }
        static GameObject UiDynamic(string name, Transform parent, Type[] components)
        {
            Type[] all = new Type[components.Length + 1]; all[0] = typeof(RectTransform); Array.Copy(components,0,all,1,components.Length);
            GameObject go = new GameObject(name,all); if(parent!=null) go.transform.SetParent(parent,false); return go;
        }
        static Text TextNode(Transform parent,string name,string value,int size,FontStyle style,TextAnchor align)
        {
            Text t=Ui(name,parent,typeof(Text)).GetComponent<Text>(); t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.text=value; t.fontSize=size; t.fontStyle=style; t.alignment=align; t.color=Color.white; return t;
        }
        static Button ButtonNode(Transform parent,string name,string label,Vector2 min,Vector2 max)
        {
            GameObject go=Ui(name,parent,typeof(Image),typeof(Button)); SetRect(go.GetComponent<RectTransform>(),min,max); go.GetComponent<Image>().color=new Color(.29f,.56f,.98f,1f); Text t=TextNode(go.transform,"Label",label,17,FontStyle.Bold,TextAnchor.MiddleCenter); Stretch(t.rectTransform,6f); return go.GetComponent<Button>();
        }
        static void SetRect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
        static void Stretch(RectTransform r,float p){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(p,p);r.offsetMax=new Vector2(-p,-p);}
        static GameObject Find(GameObject root,string name){Transform[] all=root.GetComponentsInChildren<Transform>(true);for(int i=0;i<all.Length;i++)if(all[i].name==name)return all[i].gameObject;return null;}
        static Type FindType(string name){foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies()){Type t=a.GetType(name,false);if(t!=null)return t;Type[] ts;try{ts=a.GetTypes();}catch{continue;}for(int i=0;i<ts.Length;i++)if(ts[i]!=null&&(ts[i].Name==name||ts[i].FullName==name))return ts[i];}return null;}
        static void Set(Component c,string field,object value){FieldInfo f=c.GetType().GetField(field,BindingFlags.Public|BindingFlags.Instance);if(f!=null){Undo.RecordObject(c,"Wire avatar search");f.SetValue(c,value);EditorUtility.SetDirty(c);}}
        static void SetMember(Component c,string name,object value){if(c==null)return;Type t=c.GetType();FieldInfo f=t.GetField(name,BindingFlags.Public|BindingFlags.Instance);if(f!=null){try{f.SetValue(c,value);}catch{}return;}PropertyInfo p=t.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(p!=null&&p.CanWrite)try{p.SetValue(c,value,null);}catch{}}
        static void SetStringMember(Component c,string name,string value){SetMember(c,name,value);}
        static void Wire(Button b,Component c,string method){if(b==null||c==null)return;MethodInfo m=c.GetType().GetMethod(method,BindingFlags.Public|BindingFlags.Instance);if(m==null)return;try{UnityAction a=(UnityAction)Delegate.CreateDelegate(typeof(UnityAction),c,m);UnityEventTools.AddPersistentListener(b.onClick,a);EditorUtility.SetDirty(b);}catch(Exception e){Debug.LogWarning("[NekoSune Avatar Search] Could not wire "+method+": "+e.Message);}}
        static void TryAddVrcUiShape(GameObject root){Type t=FindType("VRC.SDK3.Components.VRCUiShape");if(t==null)t=FindType("VRCUiShape");if(t==null)t=FindType("VRC_UiShape");if(t!=null&&root.GetComponent(t)==null)Undo.AddComponent(root,t);}
        static void CopyRuntime(){string src=Path.GetFullPath(Path.Combine("Packages/com.nekosune.world-avatar-search/Templates/Runtime/NekoAvatarSearchBrowser.cs.txt"));EnsureFolder(GeneratedFolder);string dst=Path.Combine(Directory.GetParent(Application.dataPath).FullName,GeneratedScriptPath.Replace('/',Path.DirectorySeparatorChar));File.Copy(src,dst,true);AssetDatabase.Refresh();}
        static string EnsureFolder(string path){string[]p=path.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string n=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(n))AssetDatabase.CreateFolder(cur,p[i]);cur=n;}return cur;}
    }
}

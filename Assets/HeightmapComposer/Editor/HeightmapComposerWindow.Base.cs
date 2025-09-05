//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditorInternal;
//using UnityEngine;
//using System.Linq;

//namespace HeightmapComposer.Editor
//{
//    public partial class HeightmapComposerWindow
//    {
//        SerializedObject so;
//        HeightmapComposer.HeightmapCompositeCollection coll;
//        ReorderableList list;

//        Texture2D previewTex;
//        RenderTexture previewRT;
//        Vector2 scroll;
//        double nextPreviewAt = 0;
//        bool dirtyPreview = true;

//        int previewRes = 512;
//        bool useGPUIfPossible = true;
//        ComputeShader computeShader;

//        bool paintMode = false;
//        float brushRadiusPx = 32f;
//        float brushFalloff = 0.5f;
//        float brushStrength = 1.0f;

//        [MenuItem("Window/Heightmaps/Composer")]
//        static void Open() => GetWindow<HeightmapComposerWindow>("Heightmap Composer");

//        void OnEnable(){ EditorApplication.update += OnEditorUpdate; }
//        void OnDisable()
//        {
//            EditorApplication.update -= OnEditorUpdate;
//            if (previewRT != null) { previewRT.Release(); DestroyImmediate(previewRT); previewRT = null; }
//            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }
//        }

//        void OnEditorUpdate()
//        {
//            if (!dirtyPreview || coll == null) return;
//            if (EditorApplication.timeSinceStartup < nextPreviewAt) return;
//            BakePreview();
//            dirtyPreview = false;
//        }

//        void OnGUI()
//        {
//            coll = (HeightmapComposer.HeightmapCompositeCollection)EditorGUILayout.ObjectField("Collection", coll, typeof(HeightmapComposer.HeightmapCompositeCollection), false);
//            computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", computeShader, typeof(ComputeShader), false);
//            useGPUIfPossible = EditorGUILayout.Toggle("Use GPU if possible", useGPUIfPossible);
//            previewRes = EditorGUILayout.IntPopup("Preview Resolution", previewRes, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });

//            if (coll == null)
//            {
//                EditorGUILayout.HelpBox("Assign a HeightmapCompositeCollection asset.", MessageType.Info);
//                return;
//            }

//            if (so == null || so.targetObject != coll) so = new SerializedObject(coll);
//            so.Update();
//            DrawLayerList();

//            GUILayout.Space(8);
//            using (new GUILayout.HorizontalScope())
//            {
//                if (GUILayout.Button("Add: Imported Texture")) AddLayer(new ImportedTextureLayer());
//                if (GUILayout.Button("Add: Referenced Texture")) AddLayer(new ReferencedTextureLayer());
//                if (GUILayout.Button("Add: Noise")) AddLayer(new NoiseGeneratorLayer());
//                if (GUILayout.Button("Add: Proxy Mesh")) AddLayer(new ProxyMeshLayer());
//            }

//            DrawExportAtResolutionUI();

//            GUILayout.Space(10);
//            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
//            using (new GUILayout.HorizontalScope())
//            {
//                if (GUILayout.Button("Refresh Preview")) MarkDirtyPreview();
//                GUI.enabled = list != null && list.index >= 0;
//                paintMode = GUILayout.Toggle(paintMode, "Mask Paint Mode", "Button", GUILayout.Width(140));
//                GUI.enabled = true;
//            }

//            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(320));
//            Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
//            GUI.Box(r, GUIContent.none);
//            if (Event.current.type == EventType.Repaint)
//            {
//                if (previewTex != null) GUI.DrawTexture(r, previewTex, ScaleMode.ScaleToFit, false);
//                else EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f));
//            }
//            EditorGUILayout.EndScrollView();

//            DrawMeshSection();

//            GUILayout.Space(8);
//            using (new GUILayout.HorizontalScope())
//            {
//                if (GUILayout.Button("Bake Full (CPU) → Texture2D"))
//                {
//                    var tex = coll.BakeFullTexture();
//                    SaveTextureAsset(tex, "Baked_HeightMask");
//                }
//                if (GUILayout.Button("Bake Full (GPU) → Texture2D"))
//                {
//                    if (!HeightmapComputeBaker.CanBakeGPU(coll) || computeShader == null)
//                        EditorUtility.DisplayDialog("GPU Bake", "Ensure all layers support GPU and assign the compute shader.", "OK");
//                    else
//                    {
//                        var tex = HeightmapComputeBaker.BakeFullGPUToTexture(coll, computeShader, -1, false);
//                        SaveTextureAsset(tex, "Baked_HeightMask_GPU");
//                    }
//                }
//            }

//            so.ApplyModifiedProperties();
//        }

//        void DrawLayerList()
//        {
//            if (list == null)
//            {
//                var layersProp = so.FindProperty("layers");
//                list = new ReorderableList(so, layersProp, true, true, false, true);
//                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Layers (top → bottom)");
//                list.drawElementCallback = (rect, index, active, focused) =>
//                {
//                    var elem = layersProp.GetArrayElementAtIndex(index);
//                    rect.height = EditorGUIUtility.singleLineHeight;
//                    EditorGUI.PropertyField(rect, elem, new GUIContent($"#{index}"), true);
//                };
//                list.elementHeightCallback = i => EditorGUI.GetPropertyHeight(so.FindProperty("layers").GetArrayElementAtIndex(i), true) + 6;
//                list.onChangedCallback = _ => { coll.AssignStableIntIds(); MarkDirtyPreview(); };
//                list.onRemoveCallback = l =>
//                {
//                    l.serializedProperty.DeleteArrayElementAtIndex(l.index);
//                    coll.AssignStableIntIds();
//                    MarkDirtyPreview();
//                };
//            }

//            EditorGUI.BeginChangeCheck();
//            list.DoLayoutList();
//            if (EditorGUI.EndChangeCheck()) { coll.AssignStableIntIds(); MarkDirtyPreview(); }
//        }

//        void AddLayer(HeightmapLayer layer)
//        {
//            layer?.EnsureGuid();
//            var layersProp = so.FindProperty("layers");
//            layersProp.arraySize++;
//            layersProp.GetArrayElementAtIndex(layersProp.arraySize - 1).managedReferenceValue = layer;
//            so.ApplyModifiedProperties();
//            coll.AssignStableIntIds();
//            MarkDirtyPreview();
//        }

//        void MarkDirtyPreview(){ dirtyPreview = true; nextPreviewAt = EditorApplication.timeSinceStartup + 0.05; }

//        // These come from the original v4 window; stubs here so the partial compiles when dropped alone.
//        void BakePreview(){}
//        void SaveTextureAsset(Texture2D tex, string defaultName){}
//    }
//}
//#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;

namespace HeightmapComposer.Editor
{
    public partial class HeightmapComposerWindow : EditorWindow
    {
        [MenuItem("Window/Heightmaps/Composer")]
        static void Open() => GetWindow<HeightmapComposerWindow>("Heightmap Composer");

        SerializedObject so;
        HeightmapComposer.HeightmapCompositeCollection coll;
        ReorderableList list;

        Texture2D previewTex;
        RenderTexture previewRT;
        Vector2 scroll;
        double nextPreviewAt = 0;
        bool dirtyPreview = true;

        int previewRes = 512;
        bool useGPUIfPossible = true;
        ComputeShader computeShader;

        // Paint state
        bool paintMode = false;
        float brushRadiusPx = 32f;
        float brushFalloff = 0.5f;
        float brushStrength = 1.0f; // 0..1 add, right-click erases

        void OnEnable() { EditorApplication.update += OnEditorUpdate; }
        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (previewRT != null) { previewRT.Release(); DestroyImmediate(previewRT); previewRT = null; }
            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }
        }

        void OnEditorUpdate()
        {
            if (!dirtyPreview || coll == null) return;
            if (EditorApplication.timeSinceStartup < nextPreviewAt) return;
            BakePreview();
            dirtyPreview = false;
        }

        void OnGUI()
        {
            coll = (HeightmapComposer.HeightmapCompositeCollection)EditorGUILayout.ObjectField("Collection", coll, typeof(HeightmapComposer.HeightmapCompositeCollection), false);
            computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", computeShader, typeof(ComputeShader), false);
            useGPUIfPossible = EditorGUILayout.Toggle("Use GPU if possible", useGPUIfPossible);
            previewRes = EditorGUILayout.IntPopup("Preview Resolution", previewRes, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });

            if (coll == null)
            {
                EditorGUILayout.HelpBox("Assign a HeightmapCompositeCollection asset.", MessageType.Info);
                return;
            }

            if (so == null || so.targetObject != coll) so = new SerializedObject(coll);
            so.Update();
            DrawLayerList();

            GUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add: Imported Texture")) AddLayer(new ImportedTextureLayer());
                if (GUILayout.Button("Add: Referenced Texture")) AddLayer(new ReferencedTextureLayer());
                if (GUILayout.Button("Add: Noise")) AddLayer(new NoiseGeneratorLayer());
                if (GUILayout.Button("Add: Proxy Mesh")) AddLayer(new ProxyMeshLayer());
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Noise Preset: Mountains")) AddLayer(new MountainsNoiseLayer());
                if (GUILayout.Button("Hills")) AddLayer(new HillsNoiseLayer());
                if (GUILayout.Button("Prairie")) AddLayer(new PrairieNoiseLayer());
                if (GUILayout.Button("Wetlands")) AddLayer(new WetlandsNoiseLayer());
                if (GUILayout.Button("Grasslands")) AddLayer(new GrasslandsNoiseLayer());
                if (GUILayout.Button("Plateaus")) AddLayer(new PlateausNoiseLayer());
                if (GUILayout.Button("Canyons")) AddLayer(new CanyonsNoiseLayer());
                if (GUILayout.Button("Plains")) AddLayer(new PlainsNoiseLayer());
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Preview")) MarkDirtyPreview();
                GUI.enabled = list != null && list.index >= 0;
                paintMode = GUILayout.Toggle(paintMode, "Mask Paint Mode", "Button", GUILayout.Width(140));
                GUI.enabled = true;
            }

            if (paintMode)
            {
                EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
                brushRadiusPx = EditorGUILayout.Slider("Radius (px)", brushRadiusPx, 1f, 256f);
                brushFalloff = EditorGUILayout.Slider("Falloff", brushFalloff, 0f, 1f);
                brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0f, 2f);
                EditorGUILayout.HelpBox("Left-drag paints (multiply), Right-drag erases (divide). Painting affects the selected layer's Paint Mask (R channel).", MessageType.Info);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(320));
            Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(r, GUIContent.none);
            if (Event.current.type == EventType.Repaint)
            {
                if (previewTex != null) GUI.DrawTexture(r, previewTex, ScaleMode.ScaleToFit, false);
                else EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f));
            }

            // Handle paint
            if (paintMode && list != null && list.index >= 0 && r.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseDrag || Event.current.type == EventType.MouseDown))
            {
                var layer = coll.layers[list.index];
                if (layer != null)
                {
                    EnsurePaintMask(layer, coll.bakeResolution);
                    // Map mouse to UV in preview rect
                    Vector2 uv = GetUVInRect(r, Event.current.mousePosition);
                    PaintOnLayerMask(layer, uv, Event.current.button == 1 ? -brushStrength : brushStrength, brushRadiusPx, brushFalloff);
                    MarkDirtyPreview();
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake Full (CPU) → Texture2D"))
                {
                    var tex = coll.BakeFullTexture();
                    SaveTextureAsset(tex, "Baked_HeightMask");
                }
                if (GUILayout.Button("Bake Full (GPU) → Texture2D"))
                {
                    if (!HeightmapComputeBaker.CanBakeGPU(coll) || computeShader == null)
                        EditorUtility.DisplayDialog("GPU Bake", "Ensure all layers support GPU and assign the compute shader.", "OK");
                    else
                    {
                        var tex = HeightmapComputeBaker.BakeFullGPUToTexture(coll, computeShader, -1, false);
                        SaveTextureAsset(tex, "Baked_HeightMask_GPU");
                    }
                }
            }

            so.ApplyModifiedProperties();
        }

        void DrawLayerList()
        {
            if (list == null)
            {
                var layersProp = so.FindProperty("layers");
                list = new ReorderableList(so, layersProp, true, true, false, true);
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Layers (top → bottom)");
                list.drawElementCallback = (rect, index, active, focused) =>
                {
                    var elem = layersProp.GetArrayElementAtIndex(index);
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, elem, new GUIContent($"#{index}"), true);
                };
                list.elementHeightCallback = i => EditorGUI.GetPropertyHeight(so.FindProperty("layers").GetArrayElementAtIndex(i), true) + 6;
                list.onChangedCallback = _ => { coll.AssignStableIntIds(); MarkDirtyPreview(); };
                list.onRemoveCallback = l =>
                {
                    l.serializedProperty.DeleteArrayElementAtIndex(l.index);
                    coll.AssignStableIntIds();
                    MarkDirtyPreview();
                };
            }

            EditorGUI.BeginChangeCheck();
            list.DoLayoutList();
            if (EditorGUI.EndChangeCheck()) { coll.AssignStableIntIds(); MarkDirtyPreview(); }
        }

        void AddLayer(HeightmapLayer layer)
        {
            layer?.EnsureGuid();
            var layersProp = so.FindProperty("layers");
            layersProp.arraySize++;
            layersProp.GetArrayElementAtIndex(layersProp.arraySize - 1).managedReferenceValue = layer;
            so.ApplyModifiedProperties();
            coll.AssignStableIntIds();
            MarkDirtyPreview();
        }

        static Vector2 GetUVInRect(Rect rect, Vector2 mouse)
        {
            float u = Mathf.InverseLerp(rect.xMin, rect.xMax, mouse.x);
            float v = 1f - Mathf.InverseLerp(rect.yMin, rect.yMax, mouse.y); // flip Y
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        void EnsurePaintMask(HeightmapLayer layer, int res)
        {
            if (layer.paint == null) layer.paint = new LayerMaskPaintOptions();
            if (!layer.paint.enablePaint) layer.paint.enablePaint = true;
            if (layer.paint.paintMask == null || layer.paint.paintMask.width != res)
            {
                var tex = new Texture2D(res, res, TextureFormat.RFloat, false, true);
                var cols = Enumerable.Repeat(new Color(1,0,0,1), res*res).ToArray(); // default mask=1 (fully on)
                tex.SetPixels(cols); tex.Apply(false, false);
                layer.paint.paintMask = tex;
                EditorUtility.SetDirty(coll);
            }
        }

        void PaintOnLayerMask(HeightmapLayer layer, Vector2 uv, float strength, float radiusPx, float falloff)
        {
            var tex = layer.paint.paintMask;
            if (tex == null) return;

            int cx = Mathf.RoundToInt(uv.x * (tex.width-1));
            int cy = Mathf.RoundToInt(uv.y * (tex.height-1));
            int r = Mathf.CeilToInt(radiusPx);
            int x0 = Mathf.Clamp(cx - r, 0, tex.width-1);
            int x1 = Mathf.Clamp(cx + r, 0, tex.width-1);
            int y0 = Mathf.Clamp(cy - r, 0, tex.height-1);
            int y1 = Mathf.Clamp(cy + r, 0, tex.height-1);

            var pixels = tex.GetPixels(x0, y0, x1-x0+1, y1-y0+1);
            int w = x1-x0+1;
            int h = y1-y0+1;
            float r2 = radiusPx*radiusPx;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int xi = x0 + i;
                int yi = y0 + j;
                float dx = xi - cx;
                float dy = yi - cy;
                float d2 = dx*dx + dy*dy;
                if (d2 > r2) continue;
                float t = 1f - Mathf.Sqrt(d2)/radiusPx;
                float fall = Mathf.SmoothStep(0f, 1f, Mathf.Pow(t, Mathf.Lerp(0.5f, 4f, falloff)));
                int idx = j*w + i;
                float current = pixels[idx].r;
                float delta = strength * fall * 0.05f; // scale
                float newVal = Mathf.Clamp01(current + delta);
                pixels[idx].r = newVal;
                pixels[idx].a = 1f;
            }
            tex.SetPixels(x0, y0, w, h, pixels);
            tex.Apply(false, false);
            EditorUtility.SetDirty(coll);
        }

        void MarkDirtyPreview(){ dirtyPreview = true; nextPreviewAt = EditorApplication.timeSinceStartup + 0.05; }

        void BakePreview()
        {
            if (previewRT != null) { previewRT.Release(); DestroyImmediate(previewRT); previewRT = null; }
            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }

            try
            {
                if (useGPUIfPossible && computeShader != null && HeightmapComputeBaker.CanBakeGPU(coll))
                {
                    previewRT = HeightmapComputeBaker.BakeFullGPU(coll, computeShader, previewRes);
                    previewTex = new Texture2D(previewRT.width, previewRT.height, TextureFormat.RGFloat, false, true);
                    var prev = RenderTexture.active;
                    RenderTexture.active = previewRT;
                    previewTex.ReadPixels(new Rect(0,0,previewRT.width,previewRT.height),0,0);
                    previewTex.Apply(false,false);
                    RenderTexture.active = prev;
                }
                else
                {
                    var data = coll.BakeFull();
                    previewTex = data.ToTexture(false);
                }
            }
            catch (System.Exception e) { Debug.LogError($"Preview bake failed: {e}"); }
        }

        static void SaveTextureAsset(Texture2D tex, string defaultName)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Baked Texture", defaultName, "asset", "Choose save location");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(tex, path);
                AssetDatabase.SaveAssets();
                EditorUtility.RevealInFinder(path);
            }
        }
    }
}
#endif

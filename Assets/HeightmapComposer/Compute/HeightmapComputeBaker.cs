using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeightmapComposer
{
    [Serializable]
    public struct GpuLayerDesc
    {
        public int type;          // 0=Tex, 1=Noise
        public int compOp;        // CompositeOp
        public int clampUV;       // bool
        public int texSlice;      // -1 if none

        public float heightScale, heightOffset, opacity, blendBias;
        public float areaMinX, areaMinY, areaMaxX, areaMaxY;

        public float frequency, persistence, lacunarity, maskThreshold, maskSmooth;
        public int octaves, outputMaskFromHeight, pad0, pad1;

        public int noiseType;               // 0..3
        public float warpAmountMeters;
        public float warpFrequency;
        public int terraceSteps;
        public float terraceStrength;

        public int maskSlice;   // pre-blurred mask slice (-1 = unused)
        public int paintSlice;  // painted mask slice (-1 = unused)
        public float padA, padB;
    }

    public static class HeightmapComputeBaker
    {
        const int MAX_LAYERS = 64;

        public static bool CanBakeGPU(HeightmapCompositeCollection coll)
        {
            if (coll == null || coll.layers == null) return false;
            foreach (var l in coll.layers)
                if (l != null && !l.SupportsGPU) return false;
            return true;
        }

        public static RenderTexture BakeFullGPU(HeightmapCompositeCollection coll, ComputeShader compositor, int resolutionOverride = -1)
        {
            if (coll == null || compositor == null) throw new ArgumentNullException();
            int res = resolutionOverride > 0 ? resolutionOverride : coll.bakeResolution;

            // Build descriptors + texture arrays
            List<GpuLayerDesc> descs;
            Texture2DArray texArray, paintArray;
            int blurredCount;
            List<int> blurDescIndices;
            BuildDescriptorsAndTextures(coll, res, out descs, out texArray, out paintArray, out blurredCount, out blurDescIndices);

            // Mask prepass (separable) if any blurred layers
            RenderTexture maskArray = null, tempArray = null;
            if (blurredCount > 0)
            {
                var prep = Resources.Load<ComputeShader>("LayerMaskPrep");
                if (prep != null)
                {
                    maskArray = new RenderTexture(res, res, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                    { dimension = TextureDimension.Tex2DArray, volumeDepth = blurredCount, enableRandomWrite = true, name = "HM_MaskArray" };
                    maskArray.Create();
                    tempArray = new RenderTexture(res, res, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                    { dimension = TextureDimension.Tex2DArray, volumeDepth = blurredCount, enableRandomWrite = true, name = "HM_MaskTemp" };
                    tempArray.Create();

                    int kCopy = prep.FindKernel("CopyMaskFromTex");
                    int kFill = prep.FindKernel("FillNoiseMask");
                    int kH = prep.FindKernel("BlurH");
                    int kV = prep.FindKernel("BlurV");
                    int g = Mathf.CeilToInt(res / 8f);

                    int blurSlice = 0;
                    for (int idx = 0; idx < coll.layers.Count; idx++)
                    {
                        var l = coll.layers[idx];
                        if (l == null || !l.blur.enableBlur) continue;

                        int dIndex = blurDescIndices[blurSlice]; // the descriptor this slice maps to

                        // update desc to point at this blur slice
                        var d = descs[dIndex];
                        d.maskSlice = blurSlice;
                        descs[dIndex] = d;

                        // common bindings
                        prep.SetInt("_Resolution", res);
                        prep.SetInt("_SliceDst", blurSlice);
                        prep.SetFloat("_SigmaPixels", Mathf.Max(0.001f, l.blur.sigmaPixels));
                        if (paintArray != null && d.paintSlice >= 0)
                        {
                            prep.SetTexture(kCopy, "_PaintMaskArray", paintArray);
                            prep.SetTexture(kFill, "_PaintMaskArray", paintArray);
                            prep.SetInt("_SlicePaint", d.paintSlice);
                        }
                        else
                        {
                            prep.SetInt("_SlicePaint", -1);
                        }

                        // Fill mask
                        if (l is NoiseGeneratorLayer n)
                        {
                            prep.SetTexture(kFill, "_MaskArray", maskArray);
                            prep.SetFloat("_TileMeters", coll.tileSizeMeters);
                            prep.SetFloat("_Frequency", n.frequency);
                            prep.SetFloat("_Persistence", n.persistence);
                            prep.SetFloat("_Lacunarity", n.lacunarity);
                            prep.SetInt("_Octaves", n.octaves);
                            prep.SetInt("_OutputMaskFromHeight", n.outputMaskFromHeight ? 1 : 0);
                            prep.SetFloat("_MaskThreshold", n.maskThreshold);
                            prep.SetFloat("_MaskSmooth", n.maskSmooth);
                            prep.SetInt("_NoiseType", (int)n.type);
                            prep.SetFloat("_WarpAmountMeters", n.warpAmountMeters);
                            prep.SetFloat("_WarpFrequency", n.warpFrequency);
                            prep.Dispatch(kFill, g, g, 1);
                        }
                        else
                        {
                            // texture/proxy
                            prep.SetTexture(kCopy, "_LayerTexArray", texArray);
                            prep.SetTexture(kCopy, "_MaskArray", maskArray);
                            prep.SetInt("_SliceSrc", d.texSlice);
                            prep.Dispatch(kCopy, g, g, 1);
                        }

                        // Blur H/V
                        prep.SetTexture(kH, "_MaskArray", maskArray);
                        prep.SetTexture(kH, "_TempArray", tempArray);
                        prep.Dispatch(kH, g, g, 1);

                        prep.SetTexture(kV, "_MaskArray", maskArray);
                        prep.SetTexture(kV, "_TempArray", tempArray);
                        prep.Dispatch(kV, g, g, 1);

                        blurSlice++;
                    }
                }
                else
                {
                    Debug.LogWarning("LayerMaskPrep.compute not found. Skipping per-layer blur.");
                }
            }

            // Composite
            var rt = new RenderTexture(res, res, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear)
            { enableRandomWrite = true, name = "HM_Final" };
            rt.Create();

            int kernel = compositor.FindKernel("CSMain");
            var layerBuffer = new ComputeBuffer(Mathf.Max(1, descs.Count), System.Runtime.InteropServices.Marshal.SizeOf<GpuLayerDesc>());
            layerBuffer.SetData(descs.ToArray());

            compositor.SetInt("_Resolution", res);
            compositor.SetFloat("_TileMeters", coll.tileSizeMeters);
            compositor.SetInt("_LayerCount", descs.Count);
            compositor.SetTexture(kernel, "_Result", rt);
            compositor.SetBuffer(kernel, "_Layers", layerBuffer);
            if (texArray != null) compositor.SetTexture(kernel, "_LayerTexArray", texArray);
            if (maskArray != null) compositor.SetTexture(kernel, "_LayerMaskArray", maskArray);
            if (paintArray != null) compositor.SetTexture(kernel, "_PaintMaskArray", paintArray);

            int groups = Mathf.CeilToInt(res / 8f);
            compositor.Dispatch(kernel, groups, groups, 1);

            layerBuffer.Dispose();
            if (maskArray != null)
            {
                maskArray.Release();
                maskArray.DiscardContents();
                maskArray.Release();
            }
            if (tempArray!=null)
            {
                tempArray.Release();
                tempArray.DiscardContents();
                maskArray.Release();
            }
            return rt;
        }

        public static Texture2D BakeFullGPUToTexture(HeightmapCompositeCollection coll, ComputeShader compositor, int resolutionOverride = -1, bool half = false)
        {
            var rt = BakeFullGPU(coll, compositor, resolutionOverride);
            var tex = new Texture2D(rt.width, rt.height, half?TextureFormat.RGHalf:TextureFormat.RGFloat, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);
            tex.Apply(false,false);
            RenderTexture.active = prev;
            return tex;
        }

        static void BuildDescriptorsAndTextures(
            HeightmapCompositeCollection coll, int bakeRes,
            out List<GpuLayerDesc> descs,
            out Texture2DArray texArray,
            out Texture2DArray paintArray,
            out int blurredCount,
            out List<int> blurDescIndices)
        {
            descs = new List<GpuLayerDesc>(Mathf.Min(64, coll.layers.Count));
            var texSources = new List<Texture2D>();
            var paintSources = new List<Texture2D>();
            var paintSliceOfDesc = new List<int>(); // parallel to descs
            blurredCount = 0;
            blurDescIndices = new List<int>();

            // Collect and prepare sources
            foreach (var l in coll.layers)
            {
                if (l == null) continue;
                if (l is ProxyMeshLayer pm) l.Prepare(bakeRes, bakeRes, coll.tileSizeMeters);
            }

            // Build descriptors and pack texture sources
            int texSliceCursor = 0;
            foreach (var l in coll.layers)
            {
                if (l == null) continue;
                var d = new GpuLayerDesc
                {
                    compOp = (int)l.composite.op,
                    heightScale = l.mapping.heightScale,
                    heightOffset = l.mapping.heightOffset,
                    opacity = l.mapping.opacity,
                    blendBias = l.composite.blendBias,
                    areaMinX = l.mapping.areaMeters.xMin,
                    areaMinY = l.mapping.areaMeters.yMin,
                    areaMaxX = l.mapping.areaMeters.xMax,
                    areaMaxY = l.mapping.areaMeters.yMax,
                    clampUV = l.mapping.clampUV ? 1 : 0,
                    texSlice = -1,
                    frequency = 0, persistence = 0, lacunarity = 0, maskThreshold = 0, maskSmooth = 0,
                    octaves = 0, outputMaskFromHeight = 0, pad0 = 0, pad1 = 0,
                    noiseType = 0, warpAmountMeters = 0, warpFrequency = 0, terraceSteps = 0, terraceStrength = 0,
                    maskSlice = -1, paintSlice = -1, padA=0, padB=0
                };

                // Assign paint slice if any
                if (l.paint != null && l.paint.enablePaint && l.paint.paintMask != null)
                {
                    paintSources.Add(l.paint.paintMask);
                    d.paintSlice = paintSources.Count - 1;
                }

                if (l is NoiseGeneratorLayer n)
                {
                    d.type = 1;
                    d.frequency = n.frequency;
                    d.octaves = n.octaves;
                    d.persistence = n.persistence;
                    d.lacunarity = n.lacunarity;
                    d.outputMaskFromHeight = n.outputMaskFromHeight ? 1 : 0;
                    d.maskThreshold = n.maskThreshold;
                    d.maskSmooth = n.maskSmooth;
                    d.noiseType = (int)n.type;
                    d.warpAmountMeters = n.warpAmountMeters;
                    d.warpFrequency = n.warpFrequency;
                    d.terraceSteps = n.terraceSteps;
                    d.terraceStrength = n.terraceStrength;
                }
                else if (l is ImportedTextureLayer it)
                {
                    d.type = 0;
                    if (it.source != null) { texSources.Add(it.source); d.texSlice = texSliceCursor++; }
                }
                else if (l is ReferencedTextureLayer rtLayer)
                {
                    d.type = 0;
#if UNITY_EDITOR
                    Texture2D resolved = null;
                    if (!string.IsNullOrEmpty(rtLayer.assetGuid))
                        resolved = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(rtLayer.assetGuid));
                    var src = resolved != null ? resolved : rtLayer.fallbackTexture;
#else
                    var src = rtLayer.fallbackTexture;
#endif
                    if (src != null) { texSources.Add(src); d.texSlice = texSliceCursor++; }
                }
                else if (l is ProxyMeshLayer pm)
                {
                    d.type = 0;
                    var src = pm.GetGpuTexture();
                    if (src != null) { texSources.Add(src); d.texSlice = texSliceCursor++; }
                }
                else continue;

                // Track blur slices
                if (l.blur != null && l.blur.enableBlur)
                {
                    blurDescIndices.Add(descs.Count);
                    blurredCount++;
                }

                descs.Add(d);
            }

            // Pack arrays
            texArray = null;
            if (texSources.Count > 0)
            {
                texArray = new Texture2DArray(bakeRes, bakeRes, texSources.Count, TextureFormat.RGFloat, false, true);
                for (int i = 0; i < texSources.Count; i++)
                {
                    var src = texSources[i];
                    var tmp = new Texture2D(bakeRes, bakeRes, TextureFormat.RGFloat, false, true);
                    var cols = new Color[bakeRes * bakeRes];
                    for (int y = 0; y < bakeRes; y++)
                    {
                        float v = (y + 0.5f) / bakeRes;
                        for (int x = 0; x < bakeRes; x++)
                        {
                            float u = (x + 0.5f) / bakeRes;
                            Color c = src != null ? src.GetPixelBilinear(u, v) : new Color(0,0,0,0);
                            float g = (src != null && (src.format == TextureFormat.RGFloat || src.format == TextureFormat.RGHalf)) ? c.g : (src!=null ? 1f : 0f);
                            cols[y*bakeRes + x] = new Color(c.r, g, 0, 1);
                        }
                    }
                    tmp.SetPixels(cols); tmp.Apply(false);
                    Graphics.CopyTexture(tmp, 0, 0, texArray, i, 0);
                }
                texArray.Apply(false);
            }

            paintArray = null;
            if (paintSources.Count > 0)
            {
                paintArray = new Texture2DArray(bakeRes, bakeRes, paintSources.Count, TextureFormat.RFloat, false, true);
                for (int i = 0; i < paintSources.Count; i++)
                {
                    var src = paintSources[i];
                    var tmp = new Texture2D(bakeRes, bakeRes, TextureFormat.RFloat, false, true);
                    var cols = new Color[bakeRes*bakeRes];
                    for (int y = 0; y < bakeRes; y++)
                    {
                        float v = (y + 0.5f) / bakeRes;
                        for (int x = 0; x < bakeRes; x++)
                        {
                            float u = (x + 0.5f) / bakeRes;
                            float r = src != null ? src.GetPixelBilinear(u, v).r : 1f;
                            cols[y*bakeRes + x] = new Color(r, 0, 0, 1);
                        }
                    }
                    tmp.SetPixels(cols); tmp.Apply(false);
                    Graphics.CopyTexture(tmp, 0, 0, paintArray, i, 0);
                }
                paintArray.Apply(false);
            }
        }
    }
}

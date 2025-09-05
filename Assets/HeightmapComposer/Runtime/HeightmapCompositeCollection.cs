using System.Collections.Generic;
using UnityEngine;

namespace HeightmapComposer
{
    [CreateAssetMenu(fileName="HeightmapCompositeCollection", menuName="Heightmaps/Composite Collection")]
    public class HeightmapCompositeCollection : ScriptableObject
    {
        public float tileSizeMeters = 2000f;
        public int bakeResolution = 2048;

        [SerializeReference] public List<HeightmapLayer> layers = new List<HeightmapLayer>();

        [ContextMenu("Assign Stable Int IDs")]
        public void AssignStableIntIds()
        {
            for(int i=0;i<layers.Count;i++){ var l=layers[i]; if(l==null) continue; l.EnsureGuid(); l.SetStableIntId(i); }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void PrepareAll(){ foreach(var l in layers) l?.Prepare(bakeResolution,bakeResolution,tileSizeMeters); }

        public HeightmapData BakeFull()
        {
            PrepareAll();
            var data = new HeightmapData(bakeResolution,bakeResolution); data.Clear(0,0);
            for(int y=0;y<bakeResolution;y++){ float v=(y+0.5f)/bakeResolution;
                for(int x=0;x<bakeResolution;x++){ float u=(x+0.5f)/bakeResolution;
                    float accumH = data.GetHeight(x,y);
                    float accumM = data.GetMask(x,y);
                    foreach(var layer in layers)
                    {
                        if(layer==null) continue;
                        layer.SampleUV(u,v,out float lh,out float lm,tileSizeMeters);
                        if (lm <= 0f) continue;
                        ApplyComposite(layer.composite, ref accumH, ref accumM, lh, lm);
                    }
                    data.Set(x,y,accumH,accumM);
                }
            }
            return data;
        }

        static void ApplyComposite(CompositeOptions opt, ref float accumH, ref float accumM, float layerH, float layerM)
        {
            float a = Mathf.Clamp01(layerM * Mathf.Max(1e-6f, opt.blendBias));
            switch(opt.op)
            {
                case CompositeOp.Equals:  accumH = Mathf.Lerp(accumH, layerH, a); break;
                case CompositeOp.Min:     accumH = Mathf.Lerp(accumH, Mathf.Min(accumH, layerH), a); break;
                case CompositeOp.Max:     accumH = Mathf.Lerp(accumH, Mathf.Max(accumH, layerH), a); break;
                case CompositeOp.Average: accumH = Mathf.Lerp(accumH, 0.5f*(accumH + layerH), a); break;
                case CompositeOp.Add:     accumH = accumH + layerH * a; break;
                case CompositeOp.Subtract:accumH = accumH - layerH * a; break;
            }
            accumM = Mathf.Max(accumM, layerM);
        }

        public Texture2D BakeFullTexture(bool half=false){ return BakeFull().ToTexture(half); }
    }
}

using System;
using UnityEngine;

namespace HeightmapComposer
{
    [Serializable]
    public abstract class HeightmapLayer
    {
        [SerializeField] private string guid;
        [SerializeField] private int stableIntId;
        public string GuidString => guid;
        public int StableIntId => stableIntId;
        public void EnsureGuid(){ if(string.IsNullOrEmpty(guid)) guid = System.Guid.NewGuid().ToString("N"); }
        public void SetStableIntId(int id)=>stableIntId=id;

        [Header("Placement")]
        public TileCoord tile;
        public Vector3 localPosition;

        [Header("Options")]
        public LayerMappingOptions mapping = new LayerMappingOptions();
        public CompositeOptions composite = new CompositeOptions();

        [Header("Blur (Mask)")]
        public LayerBlurOptions blur = new LayerBlurOptions();

        [Header("Paint (Mask)")]
        public LayerMaskPaintOptions paint = new LayerMaskPaintOptions();

        public abstract bool SupportsGPU { get; }
        public virtual void Prepare(int targetWidth, int targetHeight, float tileSizeMeters) { }
        public abstract void SampleUV(float u, float v, out float h, out float m, float tileSizeMeters);

        protected bool TryMapUVToLayer(float u, float v, float tileSizeMeters, out float lu, out float lv)
        {
            var a = mapping.areaMeters;
            float xM = u * tileSizeMeters;
            float yM = v * tileSizeMeters;
            if (!a.Contains(new Vector2(xM, yM)))
            {
                if (mapping.clampUV) { xM = Mathf.Clamp(xM, a.xMin, a.xMax); yM = Mathf.Clamp(yM, a.yMin, a.yMax); }
                else { lu = lv = 0f; return false; }
            }
            lu = (xM - a.xMin) / Mathf.Max(1e-6f, a.width);
            lv = (yM - a.yMin) / Mathf.Max(1e-6f, a.height);
            return true;
        }

        protected (float h, float m) ApplyHeightPost(float h, float m)
        { h = h * mapping.heightScale + mapping.heightOffset; m = Mathf.Clamp01(m * mapping.opacity); return (h, m); }

        protected float ApplyPaintMask(float u, float v, float tileSizeMeters, float m)
        {
            if (paint == null || !paint.enablePaint || paint.paintMask == null) return m;
            if (!TryMapUVToLayer(u, v, tileSizeMeters, out float lu, out float lv)) return 0f;
            var tex = paint.paintMask;
            if (!tex.isReadable) return m;
            float pm = tex.GetPixelBilinear(lu, lv).r;
            return Mathf.Clamp01(m * pm);
        }
    }
}

using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class ReferencedTextureLayer : HeightmapLayer
    {
        public string assetGuid;
        public Texture2D fallbackTexture;
#if UNITY_EDITOR
        private Texture2D resolved;
#endif
        public override bool SupportsGPU =>
#if UNITY_EDITOR
            (!string.IsNullOrEmpty(assetGuid) && UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid) != null) || fallbackTexture != null;
#else
            fallbackTexture != null;
#endif
        public override void Prepare(int targetWidth,int targetHeight,float tileSizeMeters)
        {
            EnsureGuid();
#if UNITY_EDITOR
            if(!string.IsNullOrEmpty(assetGuid))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
                resolved = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
#endif
        }
        public override void SampleUV(float u,float v,out float h,out float m,float tileSizeMeters)
        {
#if UNITY_EDITOR
            var tex = resolved!=null?resolved:fallbackTexture;
#else
            var tex = fallbackTexture;
#endif
            if(!TryMapUVToLayer(u,v,tileSizeMeters,out float lu,out float lv) || tex==null || !tex.isReadable){h=0;m=0;return;}
            var c=tex.GetPixelBilinear(lu,lv);
            float mask=(tex.format==TextureFormat.RGFloat||tex.format==TextureFormat.RGHalf)?c.g:1f;
            mask = ApplyPaintMask(u,v,tileSizeMeters, mask);
            h=c.r; m=Mathf.Clamp01(mask); (h,m)=ApplyHeightPost(h,m);
        }
    }
}

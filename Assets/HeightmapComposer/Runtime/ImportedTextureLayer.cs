using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class ImportedTextureLayer : HeightmapLayer
    {
        public Texture2D source;
        public override bool SupportsGPU => source != null;
        public override void Prepare(int targetWidth,int targetHeight,float tileSizeMeters){ EnsureGuid(); }
        public override void SampleUV(float u,float v,out float h,out float m,float tileSizeMeters)
        {
            if(!TryMapUVToLayer(u,v,tileSizeMeters,out float lu,out float lv) || source==null || !source.isReadable){h=0;m=0;return;}
            var c=source.GetPixelBilinear(lu,lv);
            float mask=(source.format==TextureFormat.RGFloat||source.format==TextureFormat.RGHalf)?c.g:1f;
            mask = ApplyPaintMask(u,v,tileSizeMeters, mask);
            h=c.r; m=Mathf.Clamp01(mask); (h,m)=ApplyHeightPost(h,m);
        }
    }
}

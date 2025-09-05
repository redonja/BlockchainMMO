using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class ProxyMeshLayer : HeightmapLayer
    {
        public Mesh mesh;
        public Matrix4x4 meshLocalToTile = Matrix4x4.identity;
        [Header("Vertical Range")] public bool autoFitVertical=true; public float verticalPadding=5f; public float heightMin=-1000f; public float heightMax=3000f;
        [SerializeField,HideInInspector] private Texture2D gpuBaked;
        public override bool SupportsGPU => mesh!=null;

        public override void Prepare(int targetWidth,int targetHeight,float tileSizeMeters)
        {
            EnsureGuid();
            if(autoFitVertical && mesh!=null)
            {
                var b=mesh.bounds; float minY=float.PositiveInfinity,maxY=float.NegativeInfinity; Vector3 mn=b.min,mx=b.max;
                for(int xi=0;xi<2;xi++) for(int yi=0;yi<2;yi++) for(int zi=0;zi<2;zi++)
                { var p=new Vector3(xi==0?mn.x:mx.x, yi==0?mn.y:mx.y, zi==0?mn.z:mx.z); var w=meshLocalToTile.MultiplyPoint3x4(p); if(w.y<minY)minY=w.y; if(w.y>maxY)maxY=w.y; }
                heightMin=minY-Mathf.Abs(verticalPadding); heightMax=maxY+Mathf.Abs(verticalPadding);
            }
            if(mesh!=null && (gpuBaked==null || gpuBaked.width!=targetWidth))
            {
                gpuBaked = ProxyMeshHeightBaker.BakeProxyMeshToTexture(mesh, meshLocalToTile, mapping.areaMeters, targetWidth, tileSizeMeters, heightMin, heightMax);
                gpuBaked.wrapMode=TextureWrapMode.Clamp; gpuBaked.filterMode=FilterMode.Bilinear;
            }
        }
        public Texture2D GetGpuTexture()=>gpuBaked;

        public override void SampleUV(float u,float v,out float h,out float m,float tileSizeMeters)
        {
            var tex=gpuBaked;
            if(tex!=null && TryMapUVToLayer(u,v,tileSizeMeters,out float lu,out float lv))
            {
                var c=tex.GetPixelBilinear(lu,lv);
                float mask = c.g;
                mask = ApplyPaintMask(u,v,tileSizeMeters, mask);
                h=c.r; m=mask; (h,m)=ApplyHeightPost(h,m); return;
            }
            h=0; m=0;
        }
    }
}

using UnityEngine;
using System;

namespace HeightmapComposer
{
    public static class ProxyMeshHeightBaker
    {
        static Material sMat;
        static Material GetMat(){
            if(sMat==null){
                var sh=Shader.Find("HeightmapComposer/ProxyMeshHeight");
                if(sh==null) throw new Exception("Shader 'HeightmapComposer/ProxyMeshHeight' not found.");
                sMat=new Material(sh){hideFlags=HideFlags.HideAndDontSave};
            }
            return sMat;
        }

        public static Texture2D BakeProxyMeshToTexture(Mesh mesh, Matrix4x4 localToTile, Rect areaMeters, int resolution, float tileSizeMeters=2000f, float heightMin=-1000f, float heightMax=3000f)
        {
            if(mesh==null) throw new ArgumentNullException(nameof(mesh));
            if(resolution<=0) throw new ArgumentOutOfRangeException(nameof(resolution));

            var mat=GetMat();
            var rt=new RenderTexture(resolution,resolution,32,RenderTextureFormat.RGFloat,RenderTextureReadWrite.Linear){enableRandomWrite=false,useMipMap=false,autoGenerateMips=false,antiAliasing=1,name="ProxyMeshHeight_RT"};
            rt.Create();

            mat.SetMatrix("_ObjectToWorld",localToTile);
            mat.SetVector("_AreaMinMax", new Vector4(areaMeters.xMin, areaMeters.yMin, areaMeters.xMax, areaMeters.yMax));
            mat.SetVector("_TileMeters", new Vector2(tileSizeMeters, tileSizeMeters));
            mat.SetVector("_HeightMinMax", new Vector2(heightMin, heightMax));
            mat.SetFloat("_WriteMask",1f);

            var prev=RenderTexture.active;
            Graphics.SetRenderTarget(rt); GL.Clear(true,true,new Color(0,0,0,0),1f);
            mat.SetPass(0); Graphics.DrawMeshNow(mesh, Matrix4x4.identity);

            var tex=new Texture2D(resolution,resolution,TextureFormat.RGFloat,false,true){name="ProxyMeshHeight_Tex"};
            RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,resolution,resolution),0,0,false); tex.Apply(false,false); RenderTexture.active=prev;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }
    }
}

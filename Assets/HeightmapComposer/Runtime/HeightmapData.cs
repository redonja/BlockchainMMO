using UnityEngine;

namespace HeightmapComposer
{
    public class HeightmapData
    {
        public readonly int width, height;
        private readonly float[] r;
        private readonly float[] g;
        public HeightmapData(int width,int height){this.width=width;this.height=height;r=new float[width*height];g=new float[width*height];}
        int Idx(int x,int y)=>y*width+x;
        public void Set(int x,int y,float h,float m){int i=Idx(x,y); r[i]=h; g[i]=Mathf.Clamp01(m);}
        public float GetHeight(int x,int y)=>r[Idx(x,y)];
        public float GetMask(int x,int y)=>g[Idx(x,y)];
        public void Clear(float h=0,float m=0){for(int i=0;i<r.Length;i++){r[i]=h; g[i]=Mathf.Clamp01(m);}}
        public Texture2D ToTexture(bool half=false)
        {
            var tex=new Texture2D(width,height,half?TextureFormat.RGHalf:TextureFormat.RGFloat,false,true);
            var cols=new Color[width*height];
            for(int i=0;i<cols.Length;i++) cols[i]=new Color(r[i],g[i],0,1);
            tex.SetPixels(cols); tex.Apply(false,false); return tex;
        }
    }
}

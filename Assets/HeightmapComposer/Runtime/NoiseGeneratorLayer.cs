using UnityEngine;

namespace HeightmapComposer
{
    [System.Serializable]
    public class NoiseGeneratorLayer : HeightmapLayer
    {
        [Header("Type")] public NoiseType type = NoiseType.FBM;

        [Header("Fractal Base")]
        public int seed = 12345;
        [Min(0f)] public float frequency = 0.0015f;
        [Range(1,12)] public int octaves = 4;
        [Range(0f,1f)] public float persistence = 0.5f;
        [Min(1f)] public float lacunarity = 2f;

        [Header("Warp (DomainWarpFBM only)")]
        [Min(0f)] public float warpAmountMeters = 0f;
        [Min(0f)] public float warpFrequency = 0.0025f;

        [Header("Terrace (optional)")]
        [Min(0)] public int terraceSteps = 0;
        [Range(0f,1f)] public float terraceStrength = 1f;

        [Header("Mask")]
        public bool outputMaskFromHeight = false;
        [Range(0f,1f)] public float maskThreshold = 0.5f;
        [Range(0.001f,0.5f)] public float maskSmooth = 0.05f;

        public override bool SupportsGPU => true;

        private System.Random rng; private Vector2 offsetMain, offsetWarp;

        public override void Prepare(int targetWidth, int targetHeight, float tileSizeMeters)
        {
            EnsureGuid();
            rng = new System.Random(seed);
            offsetMain = new Vector2((float)rng.NextDouble()*10000f,(float)rng.NextDouble()*10000f);
            offsetWarp = new Vector2((float)rng.NextDouble()*10000f,(float)rng.NextDouble()*10000f);
        }

        public override void SampleUV(float u,float v,out float h,out float m,float tileSizeMeters)
        {
            if(!TryMapUVToLayer(u,v,tileSizeMeters,out _,out _)){ h=0;m=0;return; }
            float xm=u*tileSizeMeters, ym=v*tileSizeMeters;
            Vector2 pMeters=new Vector2(xm,ym);
            if(type==NoiseType.DomainWarpFBM && warpAmountMeters>0f)
            {
                Vector2 w=WarpVector(pMeters*warpFrequency+offsetWarp,2);
                pMeters+=w*warpAmountMeters;
            }
            float n=Fractal(pMeters*frequency+offsetMain,octaves,persistence,lacunarity,type);
            float h01=Mathf.Clamp01(n*0.5f+0.5f);
            if(terraceSteps>0) h01=Terrace(h01,terraceSteps,terraceStrength);

            float mask = outputMaskFromHeight ? SmoothMask(h01,maskThreshold,maskSmooth) : 1f;
            mask = ApplyPaintMask(u,v,tileSizeMeters,mask);

            h=h01; m=mask; (h,m)=ApplyHeightPost(h,m);
        }

        public void ApplyPreset(TerrainNoisePreset preset)
        {
            switch(preset)
            {
                case TerrainNoisePreset.Mountains:
                    type=NoiseType.Ridged; frequency=0.0012f; octaves=6; persistence=0.45f; lacunarity=2.1f;
                    warpAmountMeters=120f; warpFrequency=0.002f; terraceSteps=6; terraceStrength=0.35f;
                    break;
                case TerrainNoisePreset.Hills:
                    type=NoiseType.Billow; frequency=0.0022f; octaves=5; persistence=0.55f; lacunarity=2.0f;
                    warpAmountMeters=40f; warpFrequency=0.003f; terraceSteps=0; terraceStrength=0f;
                    break;
                case TerrainNoisePreset.Prairie:
                    type=NoiseType.FBM; frequency=0.0007f; octaves=3; persistence=0.6f; lacunarity=1.9f;
                    warpAmountMeters=20f; warpFrequency=0.0015f; terraceSteps=0;
                    break;
                case TerrainNoisePreset.Wetlands:
                    type=NoiseType.FBM; frequency=0.0016f; octaves=4; persistence=0.5f; lacunarity=2.0f;
                    warpAmountMeters=60f; warpFrequency=0.003f; terraceSteps=0;
                    break;
                case TerrainNoisePreset.Grasslands:
                    type=NoiseType.Billow; frequency=0.0012f; octaves=4; persistence=0.6f; lacunarity=1.95f;
                    warpAmountMeters=25f; warpFrequency=0.0025f; terraceSteps=0;
                    break;
                case TerrainNoisePreset.Plateaus:
                    type=NoiseType.Ridged; frequency=0.0010f; octaves=5; persistence=0.45f; lacunarity=2.1f;
                    warpAmountMeters=80f; warpFrequency=0.0025f; terraceSteps=4; terraceStrength=0.6f;
                    break;
                case TerrainNoisePreset.Canyons:
                    type=NoiseType.Ridged; frequency=0.0018f; octaves=6; persistence=0.42f; lacunarity=2.2f;
                    warpAmountMeters=150f; warpFrequency=0.0022f; terraceSteps=8; terraceStrength=0.65f;
                    break;
                case TerrainNoisePreset.Plains:
                    type=NoiseType.FBM; frequency=0.0009f; octaves=3; persistence=0.55f; lacunarity=1.9f;
                    warpAmountMeters=10f; warpFrequency=0.0015f; terraceSteps=0;
                    break;
            }
        }

        private static float Terrace(float t,int steps,float strength){steps=Mathf.Max(1,steps); float k=Mathf.Floor(t*steps)/steps; return Mathf.Lerp(t,k,Mathf.Clamp01(strength));}
        private static float SmoothMask(float h01,float th,float sm){float t0=Mathf.Clamp01(th-sm), t1=Mathf.Clamp01(th+sm); return Mathf.SmoothStep(0,1,Mathf.InverseLerp(t0,t1,h01));}
        private static float Perlin(float x,float y)=>Mathf.PerlinNoise(x,y)*2f-1f;
        private static float Fractal(Vector2 p,int oct,float pers,float lac,NoiseType type)
        {
            float sum=0,amp=1,freq=1,norm=0;
            for(int i=0;i<oct;i++)
            {
                float n=Perlin(p.x*freq,p.y*freq);
                if(type==NoiseType.Billow) n=Mathf.Abs(n);
                else if(type==NoiseType.Ridged) n=1f-Mathf.Abs(n);
                if(type==NoiseType.Billow||type==NoiseType.Ridged) n=n*2f-1f;
                sum+=amp*n; norm+=amp; amp*=pers; freq*=lac;
            }
            return sum/Mathf.Max(1e-6f,norm);
        }
        private static Vector2 WarpVector(Vector2 p,int oct){float amp=1,freq=1; Vector2 v=Vector2.zero; for(int i=0;i<oct;i++){float nx=Mathf.PerlinNoise(p.x*freq,p.y*freq)*2f-1f; float ny=Mathf.PerlinNoise((p.x+19.17f)*freq,(p.y-7.31f)*freq)*2f-1f; v+=new Vector2(nx,ny)*amp; amp*=0.5f; freq*=2f;} return v;}
    }

    public enum TerrainNoisePreset { Mountains, Hills, Prairie, Wetlands, Grasslands, Plateaus, Canyons, Plains }
}

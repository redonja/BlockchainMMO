using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class GrasslandsNoiseLayer : NoiseGeneratorLayer
    {
        public GrasslandsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Grasslands); }
    }
}

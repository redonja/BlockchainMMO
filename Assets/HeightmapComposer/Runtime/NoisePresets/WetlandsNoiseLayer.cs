using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class WetlandsNoiseLayer : NoiseGeneratorLayer
    {
        public WetlandsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Wetlands); }
    }
}

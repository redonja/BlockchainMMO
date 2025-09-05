using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class HillsNoiseLayer : NoiseGeneratorLayer
    {
        public HillsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Hills); }
    }
}

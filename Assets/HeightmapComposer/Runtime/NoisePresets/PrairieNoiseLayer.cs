using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class PrairieNoiseLayer : NoiseGeneratorLayer
    {
        public PrairieNoiseLayer() { ApplyPreset(TerrainNoisePreset.Prairie); }
    }
}

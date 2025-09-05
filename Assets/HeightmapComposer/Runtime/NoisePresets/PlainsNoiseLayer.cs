using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class PlainsNoiseLayer : NoiseGeneratorLayer
    {
        public PlainsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Plains); }
    }
}

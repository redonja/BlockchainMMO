using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class PlateausNoiseLayer : NoiseGeneratorLayer
    {
        public PlateausNoiseLayer() { ApplyPreset(TerrainNoisePreset.Plateaus); }
    }
}

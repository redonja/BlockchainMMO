using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class CanyonsNoiseLayer : NoiseGeneratorLayer
    {
        public CanyonsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Canyons); }
    }
}

using UnityEngine;
namespace HeightmapComposer
{
    [System.Serializable]
    public class MountainsNoiseLayer : NoiseGeneratorLayer
    {
        public MountainsNoiseLayer() { ApplyPreset(TerrainNoisePreset.Mountains); }
    }
}

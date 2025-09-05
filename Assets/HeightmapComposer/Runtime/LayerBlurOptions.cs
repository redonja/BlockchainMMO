using System;

namespace HeightmapComposer
{
    [Serializable]
    public class LayerBlurOptions
    {
        public bool enableBlur = false;
        public float sigmaPixels = 1.5f;
        public bool includeDiagonals = true; // legacy UI, no-op with separable blur
    }
}

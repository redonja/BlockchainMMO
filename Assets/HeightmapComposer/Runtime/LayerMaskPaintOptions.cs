using System;
using UnityEngine;

namespace HeightmapComposer
{
    [Serializable]
    public class LayerMaskPaintOptions
    {
        public bool enablePaint = false;
        [Tooltip("Paintable mask (R channel 0..1). If null, editor will allocate one at bake resolution.")]
        public Texture2D paintMask;
    }
}

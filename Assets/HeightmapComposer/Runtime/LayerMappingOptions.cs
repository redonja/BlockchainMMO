using System;
using UnityEngine;

namespace HeightmapComposer
{
    [Serializable]
    public class LayerMappingOptions
    {
        public Rect areaMeters = new Rect(0,0,2000f,2000f);
        public float heightScale = 1f;
        public float heightOffset = 0f;
        [Range(0f,1f)] public float opacity = 1f;
        public bool clampUV = true;
    }
}

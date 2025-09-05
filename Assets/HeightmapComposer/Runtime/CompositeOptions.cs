using System;

namespace HeightmapComposer
{
    [Serializable]
    public class CompositeOptions
    {
        public CompositeOp op = CompositeOp.Equals;
        [UnityEngine.Range(0f,1f)] public float blendBias = 1f;
        public override string ToString() => $"{op} (bias {blendBias:0.##})";
    }
}

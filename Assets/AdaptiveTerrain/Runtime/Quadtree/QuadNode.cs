using UnityEngine;

namespace AdaptiveTerrain
{
    public struct QuadNode
    {
        public int id;
        public int parentId;
        public int child00, child10, child01, child11;
        public int level;
        public Vector2 xyMin;
        public float size;
        public Vector2 uvMin, uvMax;
        public bool IsLeaf => child00 < 0;
    }
}

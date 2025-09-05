using System;
using UnityEngine;

namespace AdaptiveTerrain
{
    [CreateAssetMenu(fileName="QuadTreeConfig", menuName="Terrain/QuadTree Config")]
    public class QuadTreeConfig : ScriptableObject
    {
        [Header("Layout")]
        public float worldSizeMeters = 8000f;
        public int maxDepth = 5;
        public int vertsPerSide = 129;

        [Header("Height")]
        public float heightScale = 1f;
        public float heightOffset = 0f;

        [Header("Skirts")]
        public bool enableSkirts = true;
        public float skirtHeight = 5f;

        [Header("LOD Selection")]
        public AdaptiveSelectionMode selectionMode = AdaptiveSelectionMode.CPU;
        public float[] levelSplitDistance = new float[] { 6000, 9000, 13000, 18000, 24000, 30000 };
        public float hysteresis = 1.15f;
        public bool gradientBoost = true;
        public Vector2 gradientBoostThresholds = new Vector2(0.05f, 0.15f);

        [Header("Rendering")]
        public Material terrainMaterial;
        public bool drawBoundsGizmos = false;
    }
}

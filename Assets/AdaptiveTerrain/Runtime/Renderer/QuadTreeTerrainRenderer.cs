using System.Collections.Generic;
using UnityEngine;
using HeightmapComposer;

namespace AdaptiveTerrain
{
    [ExecuteAlways]
    public class QuadTreeTerrainRenderer : MonoBehaviour
    {
        [Header("Inputs")]
        public QuadTreeConfig config;
        public HeightmapCompositeCollection heightmapCollection;
        public ComputeShader heightmapBakerCompute; // HeightmapComposer/HeightmapBaker.compute
        public ComputeShader quadTileCompute;       // AdaptiveTerrain/QuadTileBuild.compute

        [Header("Bake")]
        public int bakeResolution = 4096;
        public bool bakeOnEnable = true;

        RenderTexture fullHeightRT;
        QuadTree tree;
        List<QuadTileRuntime> tiles = new List<QuadTileRuntime>();
        HashSet<int> expandedLast = new HashSet<int>();
        HashSet<int> expandedNow= new HashSet<int>();
        Camera cam;

        void OnEnable()
        {
            cam = Camera.main;
            if (config == null) { Debug.LogWarning("QuadTreeTerrainRenderer: assign config"); return; }
            if (heightmapCollection == null || heightmapBakerCompute == null) { Debug.LogWarning("Assign HeightmapCompositeCollection + HeightmapBaker.compute"); return; }
            if (quadTileCompute == null) { Debug.LogWarning("Assign QuadTileBuild.compute"); return; }

            if (bakeOnEnable) BakeFullMap();
            BuildTree();
        }

        void OnDisable()
        {
            foreach (var t in tiles) t?.Dispose();
            tiles.Clear();
            if (fullHeightRT != null) { fullHeightRT.Release(); DestroyImmediate(fullHeightRT); fullHeightRT = null; }
        }

        void BakeFullMap()
        {
            fullHeightRT = HeightmapComputeBaker.BakeFullGPU(heightmapCollection, heightmapBakerCompute, bakeResolution);
            fullHeightRT.name = "QuadTree_FullHeight";
        }

        void BuildTree()
        {
            if (fullHeightRT == null) BakeFullMap();
            tiles.ForEach(t => t?.Dispose()); tiles.Clear();
            tree = new QuadTree(config);
        }

        void Update()
        {
            if (config == null || tree == null || quadTileCompute == null) return;
            if (cam == null) cam = Camera.main;
            Vector3 camPos = cam ? cam.transform.position : Vector3.zero;
            //removed out from lasgt param
            var leaves = tree.EnumerateLeavesCPUHysteresis(camPos, config.levelSplitDistance, config.hysteresis, expandedLast,  expandedNow);
            expandedLast = expandedNow;

            foreach (var node in leaves)
            {
                var t = GetOrCreateTile(node);
                if (config.selectionMode == AdaptiveSelectionMode.CPU)            t.BuildGPU_SelectionCPU(camPos);
                else if (config.selectionMode == AdaptiveSelectionMode.HybridRefine) t.BuildGPU_SelectionHybrid(camPos);
                else                                                                t.BuildGPU_SelectionGPU(camPos);
                t.Draw(config.terrainMaterial);
            }
        }

        QuadTileRuntime GetOrCreateTile(QuadNode node)
        {
            while (tiles.Count <= node.id) tiles.Add(null);
            var t = tiles[node.id];
            if (t == null) { t = new QuadTileRuntime(quadTileCompute, config, node, fullHeightRT); tiles[node.id] = t; }
            return t;
        }

        void OnDrawGizmosSelected()
        {
            if (config == null || !config.drawBoundsGizmos || tree == null) return;
            Gizmos.color = Color.yellow;
            foreach (var t in tiles)
            {
                if (t == null) continue;
                var n = t.node;
                Vector3 min = new Vector3(n.xyMin.x, 0, n.xyMin.y) + transform.position;
                Vector3 center = min + new Vector3(n.size*0.5f, 0, n.size*0.5f);
                Gizmos.DrawWireCube(center, new Vector3(n.size, 1, n.size));
            }
        }
    }
}

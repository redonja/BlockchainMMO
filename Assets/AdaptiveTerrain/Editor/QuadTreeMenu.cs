#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AdaptiveTerrain.Editor
{
    public static class QuadTreeMenu
    {
        [MenuItem("GameObject/Terrain/Create QuadTree Terrain Renderer", false, 10)]
        public static void Create()
        {
            var go = new GameObject("QuadTreeTerrainRenderer");
            go.AddComponent<AdaptiveTerrain.QuadTreeTerrainRenderer>();
            Selection.activeObject = go;
        }
    }
}
#endif

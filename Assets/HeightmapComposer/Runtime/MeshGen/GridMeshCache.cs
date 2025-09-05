using System.Collections.Generic;
using UnityEngine;

namespace HeightmapComposer.MeshGen
{
    public static class GridMeshCache
    {
        static readonly Dictionary<(int,int), Mesh> sCache = new Dictionary<(int,int), Mesh>();

        public static Mesh Get(int vertsPerSide, float tileSizeMeters)
        {
            var key = (vertsPerSide, Mathf.RoundToInt(tileSizeMeters));
            if (sCache.TryGetValue(key, out var m) && m != null) return m;
            m = BuildGrid(vertsPerSide, tileSizeMeters);
            sCache[key] = m;
            return m;
        }

        static Mesh BuildGrid(int vertsPerSide, float tileSize)
        {
            vertsPerSide = Mathf.Max(2, vertsPerSide);
            int quads = (vertsPerSide - 1);
            int vertCount = vertsPerSide * vertsPerSide;
            int triCount = quads * quads * 2;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var indices = new int[triCount * 3];

            float step = tileSize / (vertsPerSide - 1);
            int idx = 0;
            for (int y = 0; y < vertsPerSide; y++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    verts[idx] = new Vector3(x * step, 0f, y * step);
                    uvs[idx] = new Vector2(x / (float)(vertsPerSide - 1), y / (float)(vertsPerSide - 1));
                    idx++;
                }
            }

            int ii = 0;
            for (int y = 0; y < quads; y++)
            {
                for (int x = 0; x < quads; x++)
                {
                    int i0 = y * vertsPerSide + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertsPerSide;
                    int i3 = i2 + 1;

                    indices[ii++] = i0; indices[ii++] = i2; indices[ii++] = i1;
                    indices[ii++] = i1; indices[ii++] = i2; indices[ii++] = i3;
                }
            }

            var mesh = new Mesh { name = $"Grid_{vertsPerSide}x{vertsPerSide}_{tileSize:0}" };
            mesh.indexFormat = (vertCount > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

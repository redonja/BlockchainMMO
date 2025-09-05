using System.Collections.Generic;
using UnityEngine;

namespace HeightmapComposer
{
    public static class HeightmapComputeBakerExtensions
    {
        public static List<RenderTexture> BakeTilesGPU(HeightmapCompositeCollection coll, ComputeShader shader, int tilesX, int tilesY)
        {
            if (tilesX < 1 || tilesY < 1) tilesX = tilesY = 1;
            var full = HeightmapComputeBaker.BakeFullGPU(coll, shader);
            int res = full.width;
            int w = res / tilesX;
            int h = res / tilesY;

            var list = new List<RenderTexture>(tilesX * tilesY);
            for (int ty = 0; ty < tilesY; ty++)
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int ox = tx * w;
                    int oy = ty * h;
                    int ww = (tx == tilesX - 1) ? (res - ox) : w;
                    int hh = (ty == tilesY - 1) ? (res - oy) : h;

                    var tile = new RenderTexture(ww, hh, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear)
                    { enableRandomWrite = false, name = $"HM_Tile_{tx}_{ty}" };
                    tile.Create();

                    Graphics.CopyTexture(full, 0, 0, ox, oy, ww, hh, tile, 0, 0, 0, 0);
                    list.Add(tile);
                }
            }
            full.Release(); Object.DestroyImmediate(full);
            return list;
        }
    }
}

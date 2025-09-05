using System;

namespace HeightmapComposer
{
    [Serializable]
    public struct TileCoord
    {
        public int x;
        public int y;
        public TileCoord(int x,int y){this.x=x;this.y=y;}
        public override string ToString()=>$"({x},{y})";
    }
}

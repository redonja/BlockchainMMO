using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveTerrain
{
    public class QuadTree
    {
        public readonly System.Collections.Generic.List<QuadNode> nodes = new System.Collections.Generic.List<QuadNode>();
        readonly QuadTreeConfig cfg;

        public QuadTree(QuadTreeConfig cfg)
        {
            this.cfg = cfg;
            Build();
        }

        void Build()
        {
            nodes.Clear();
            int root = AddNode(-1, 0, new Vector2(0,0), cfg.worldSizeMeters, new Vector2(0,0), new Vector2(1,1));
            Subdivide(root, cfg.maxDepth);
        }

        int AddNode(int parent, int level, Vector2 xyMin, float size, Vector2 uvMin, Vector2 uvMax)
        {
            var n = new QuadNode
            {
                id = nodes.Count, parentId = parent, child00=-1, child10=-1, child01=-1, child11=-1,
                level=level, xyMin=xyMin, size=size, uvMin=uvMin, uvMax=uvMax
            };
            nodes.Add(n);
            return n.id;
        }
        void SetChild(int p, int idx, int c){ var n=nodes[p]; if(idx==0)n.child00=c; else if(idx==1)n.child10=c; else if(idx==2)n.child01=c; else n.child11=c; nodes[p]=n; }
        void Subdivide(int id, int depthLeft)
        {
            if (depthLeft <= 0) return;
            var p = nodes[id];
            float half = p.size*0.5f; Vector2 mid = p.xyMin + new Vector2(half, half);
            Vector2 uvMid = Vector2.Lerp(p.uvMin, p.uvMax, 0.5f);
            int c00=AddNode(id,p.level+1,p.xyMin,half,new Vector2(p.uvMin.x,p.uvMin.y), new Vector2(uvMid.x,uvMid.y));
            int c10=AddNode(id,p.level+1,new Vector2(mid.x,p.xyMin.y),half,new Vector2(uvMid.x,p.uvMin.y), new Vector2(p.uvMax.x,uvMid.y));
            int c01=AddNode(id,p.level+1,new Vector2(p.xyMin.x,mid.y),half,new Vector2(p.uvMin.x,uvMid.y), new Vector2(uvMid.x,p.uvMax.y));
            int c11=AddNode(id,p.level+1,mid,half,uvMid,p.uvMax);
            SetChild(id,0,c00); SetChild(id,1,c10); SetChild(id,2,c01); SetChild(id,3,c11);
            Subdivide(c00, depthLeft-1); Subdivide(c10, depthLeft-1); Subdivide(c01, depthLeft-1); Subdivide(c11, depthLeft-1);
        }

        //out removed from newExpanded param
        public IEnumerable<QuadNode> EnumerateLeavesCPUHysteresis(Vector3 camPos, float[] splitDist, float hysteresis, System.Collections.Generic.HashSet<int> prevExpanded,  System.Collections.Generic.HashSet<int> newExpanded)
        {
            newExpanded = new System.Collections.Generic.HashSet<int>();
            var stack = new System.Collections.Generic.Stack<int>(); stack.Push(0);
            while(stack.Count>0)
            {
                int id = stack.Pop();
                var n = nodes[id];
                int level = n.level;
                float split = (level < splitDist.Length) ? splitDist[level] : splitDist[splitDist.Length-1];
                float merge = split * Mathf.Max(1.01f, hysteresis);
                Vector2 center = n.xyMin + new Vector2(n.size*0.5f, n.size*0.5f);
                float dist = Vector2.Distance(new Vector2(camPos.x, camPos.z), center);
                bool wasExpanded = prevExpanded!=null && prevExpanded.Contains(id);
                bool wantSplit = (level < cfg.maxDepth) && (wasExpanded ? dist < merge : dist < split);
                if (wantSplit && n.child00>=0) { newExpanded.Add(id); stack.Push(n.child00); stack.Push(n.child10); stack.Push(n.child01); stack.Push(n.child11); }
                else yield return n;
            }
        }
    }
}

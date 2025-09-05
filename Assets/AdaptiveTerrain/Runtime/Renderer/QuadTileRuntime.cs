using UnityEngine;
using System;

namespace AdaptiveTerrain
{
    public class QuadTileRuntime : IDisposable
    {
        public readonly QuadNode node;
        readonly ComputeShader cs;
        readonly QuadTreeConfig cfg;
        readonly RenderTexture heightRG;

        ComputeBuffer positions;
        ComputeBuffer normals;
        ComputeBuffer indices;
        ComputeBuffer args;
        ComputeBuffer strideBuf;

        int vertsPerSide => Mathf.Max(2, cfg.vertsPerSide);
        int baseVertCount => vertsPerSide * vertsPerSide;
        int skirtVertCount => cfg.enableSkirts ? (4 * vertsPerSide) : 0;
        int vertCountTotal => baseVertCount + skirtVertCount;

        int MaxGridTriangles(int stride) => ((vertsPerSide-1)/stride)*((vertsPerSide-1)/stride)*2;
        int SkirtTriangles => cfg.enableSkirts ? 4 * (vertsPerSide-1) * 2 : 0;
        int maxIndexCount => (MaxGridTriangles(1) + SkirtTriangles) * 3;

        public QuadTileRuntime(ComputeShader quadTileCompute, QuadTreeConfig config, QuadNode node, RenderTexture fullHeight)
        {
            this.cs = quadTileCompute; this.cfg = config; this.node = node; this.heightRG = fullHeight;
            positions = new ComputeBuffer(vertCountTotal, sizeof(float)*3);
            normals   = new ComputeBuffer(vertCountTotal, sizeof(float)*3);
            indices   = new ComputeBuffer(maxIndexCount, sizeof(uint));
            args      = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
            strideBuf = new ComputeBuffer(1, sizeof(uint));
            args.SetData(new uint[]{0,1,0,0});
            strideBuf.SetData(new uint[]{1});
        }

        public void BuildGPU_SelectionCPU(Vector3 camPosWS)
        {
            strideBuf.SetData(new uint[]{ 1u });
            DispatchAll(camPosWS);
        }

        public void BuildGPU_SelectionHybrid(Vector3 camPosWS)
        {
            int k = cs.FindKernel("DecideNodeStride");
            cs.SetBuffer(k, "_Stride", strideBuf);
            cs.SetInt("_UseForcedStride", 1);
            cs.SetInt("_ForceStride", 1);
            SetCommon(k, camPosWS);
            cs.Dispatch(k,1,1,1);
            DispatchAll(camPosWS);
        }

        public void BuildGPU_SelectionGPU(Vector3 camPosWS)
        {
            int k = cs.FindKernel("DecideNodeStride");
            cs.SetBuffer(k, "_Stride", strideBuf);
            cs.SetInt("_UseForcedStride", 0);
            SetCommon(k, camPosWS);
            cs.Dispatch(k,1,1,1);
            DispatchAll(camPosWS);
        }

        void SetCommon(int kernel, Vector3 camPosWS)
        {
            cs.SetVector("_NodeUVMin", new Vector4(node.uvMin.x, node.uvMin.y, 0, 0));
            cs.SetVector("_NodeUVMax", new Vector4(node.uvMax.x, node.uvMax.y, 0, 0));
            cs.SetInt("_VertsPerSide", vertsPerSide);
            cs.SetFloat("_NodeSize", node.size);
            cs.SetFloat("_HeightScale", cfg.heightScale);
            cs.SetFloat("_HeightOffset", cfg.heightOffset);
            cs.SetInt("_EnableSkirts", cfg.enableSkirts ? 1 : 0);
            cs.SetFloat("_SkirtHeight", cfg.skirtHeight);
            cs.SetInt("_GradientBoost", cfg.gradientBoost ? 1 : 0);
            cs.SetVector("_GradientBoostThresholds", cfg.gradientBoostThresholds);
            cs.SetTexture(kernel, "_HeightRG", heightRG);
        }

        void DispatchAll(Vector3 camPosWS)
        {
            int kVerts = cs.FindKernel("BuildNodeVertices");
            int kInds  = cs.FindKernel("BuildNodeIndicesAndArgs");

            SetCommon(kVerts, camPosWS);
            cs.SetBuffer(kVerts, "_Positions", positions);
            cs.SetBuffer(kVerts, "_Normals", normals);
            int groups = Mathf.CeilToInt(vertsPerSide/8f);
            cs.Dispatch(kVerts, groups, groups, 1);

            cs.SetInt("_VertsPerSide", vertsPerSide);
            cs.SetBuffer(kInds, "_Stride", strideBuf);
            cs.SetBuffer(kInds, "_Indices", indices);
            cs.SetBuffer(kInds, "_Args", args);
            cs.SetInt("_BaseVertCount", baseVertCount);
            cs.Dispatch(kInds, groups, groups, 1);
        }

        public void Draw(Material mat)
        {
            if (mat == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetBuffer("_Positions", positions);
            mpb.SetBuffer("_Normals", normals);
            mpb.SetBuffer("_Indices", indices);
            Matrix4x4 l2w = Matrix4x4.TRS(new Vector3(node.xyMin.x, 0, node.xyMin.y), Quaternion.identity, Vector3.one);
            mpb.SetMatrix("_LocalToWorld", l2w);
            var bounds = new Bounds(new Vector3(node.xyMin.x + node.size/2f, 0, node.xyMin.y + node.size/2f),
                                    new Vector3(node.size, 20000f, node.size));
            Graphics.DrawProceduralIndirect(mat, bounds, MeshTopology.Triangles, args, 0, null, mpb);
        }

        public void Dispose()
        {
            positions?.Dispose(); normals?.Dispose(); indices?.Dispose(); args?.Dispose(); strideBuf?.Dispose();
        }
    }
}

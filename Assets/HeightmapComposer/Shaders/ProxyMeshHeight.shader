Shader "HeightmapComposer/ProxyMeshHeight"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off ZWrite On ZTest Less Blend Off

        Pass
        {
            HLSLINCLUDE
            #include "UnityCG.cginc"
            float4x4 _ObjectToWorld;
            float4 _AreaMinMax;
            float2 _TileMeters;
            float2 _HeightMinMax;
            float  _WriteMask;

            struct appdata { float3 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float worldY : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 w = mul(_ObjectToWorld, float4(v.vertex,1)).xyz;
                float u = saturate((w.x - _AreaMinMax.x) / max(1e-6, (_AreaMinMax.z - _AreaMinMax.x)));
                float v2 = saturate((w.z - _AreaMinMax.y) / max(1e-6, (_AreaMinMax.w - _AreaMinMax.y)));
                float xClip = lerp(-1.0, 1.0, u);
                float yClip = lerp(-1.0, 1.0, v2);
                float h01 = saturate((w.y - _HeightMinMax.x) / max(1e-6, (_HeightMinMax.y - _HeightMinMax.x)));
                float zClip = 1.0 - h01;
                o.pos = float4(xClip, yClip, zClip, 1.0);
                o.worldY = w.y; return o;
            }

            float2 frag (v2f i) : SV_Target
            { return float2(i.worldY, _WriteMask); }
            ENDHLSL
        }
    }
    FallBack Off
}

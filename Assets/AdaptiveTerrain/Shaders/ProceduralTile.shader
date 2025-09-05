Shader "AdaptiveTerrain/ProceduralTile"
{
    Properties { _Color ("Tint", Color) = (0.6,0.7,0.8,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLINCLUDE
            #include "UnityCG.cginc"
            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float3> _Normals;
            StructuredBuffer<uint>   _Indices;
            float4x4 _LocalToWorld;
            float4 _Color;

            struct v2f { float4 pos:SV_POSITION; float3 nrm:TEXCOORD0; };

            v2f vert(uint vid:SV_VertexID)
            {
                uint vi = _Indices[vid];
                float3 p = _Positions[vi];
                float3 n = _Normals[vi];
                v2f o;
                float4 wp = mul(_LocalToWorld, float4(p,1));
                float3 wn = normalize(mul((float3x3)_LocalToWorld, n));
                o.pos = UnityObjectToClipPos(wp);
                o.nrm = wn;
                return o;
            }

            float4 frag(v2f i):SV_Target
            {
                float l = saturate(dot(normalize(i.nrm), normalize(float3(0.25,0.75,0.6))));
                return float4(_Color.rgb * (0.3 + 0.7*l), 1);
            }
            ENDHLSL
        }
    }
}

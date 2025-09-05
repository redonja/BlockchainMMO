Shader "HeightmapComposer/TileGridDisplace"
{
    Properties{
        _HeightTex ("Height RG texture", 2D) = "black" {}
        _HeightScale ("Height Scale", Float) = 1
        _HeightOffset("Height Offset", Float) = 0
        _HeightChannelMask("Channel Mask (R/G pick)", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLINCLUDE
            #include "UnityCG.cginc"
            sampler2D _HeightTex;
            float4 _HeightTex_TexelSize;
            float _HeightScale, _HeightOffset;
            float4 _HeightChannelMask;

            struct appdata {
                float3 pos : POSITION;
                float2 uv  : TEXCOORD0;
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float3 nrm : TEXCOORD0;
            };

            float SampleHeight(float2 uv)
            {
                float4 rg = tex2D(_HeightTex, uv);
                float h = dot(rg, _HeightChannelMask); // default R
                return h * _HeightScale + _HeightOffset;
            }

            v2f vert (appdata v)
            {
                v2f o;
                float h = SampleHeight(v.uv);
                float3 wp = v.pos + float3(0,h,0);
                o.pos = UnityObjectToClipPos(wp);

                float2 du = float2(_HeightTex_TexelSize.x, 0);
                float2 dv = float2(0, _HeightTex_TexelSize.y);
                float hx = SampleHeight(v.uv + du) - SampleHeight(v.uv - du);
                float hz = SampleHeight(v.uv + dv) - SampleHeight(v.uv - dv);
                float3 n = normalize(cross(float3(1,hx,0), float3(0,hz,1)));
                o.nrm = n;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float nd = saturate(dot(normalize(i.nrm), float3(0.25,0.75,0.6)));
                return float4(nd.xxx,1);
            }
            ENDHLSL
        }
    }
}

Shader "GaussianSplatting/ProceduralBillboard"
{
	Properties
    {
        _AlphaScale ("Alpha Scale", Float) = 1.0
        _GaussianSharpness ("Gaussian Sharpness", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct GaussianData
            {
                float3 position;
                float radius;
                float4 color;
            };

            StructuredBuffer<GaussianData> _Gaussians;

            float _AlphaScale;
            float _GaussianSharpness;
            float4x4 _LocalToWorld;
            float3 _CameraRightWS;
            float3 _CameraUpWS;

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 quadUV : TEXCOORD0;
                float4 color : COLOR;
            };

            float2 GetCornerUV(uint vertexID)
            {
                uint local = vertexID % 6;

                if (local == 0) return float2(-1, -1);
                if (local == 1) return float2(1, -1);
                if (local == 2) return float2(1, 1);
                if (local == 3) return float2(-1, -1);
                if (local == 4) return float2(1, 1);
                return float2(-1, 1);
            }

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                GaussianData g = _Gaussians[instanceID];
                float2 quadUV = GetCornerUV(vertexID);

                float3 centerWS = mul(_LocalToWorld, float4(g.position, 1.0)).xyz;
                float3 offsetWS =
                    _CameraRightWS * (quadUV.x * g.radius) +
                    _CameraUpWS * (quadUV.y * g.radius);

                float3 positionWS = centerWS + offsetWS;

                o.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                o.quadUV = quadUV;
                o.color = saturate(g.color);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.quadUV, i.quadUV);

                if (r2 > 1.0)
                {
                    discard;
                }

                float gaussian = exp(-r2 * _GaussianSharpness);
                float alpha = i.color.a * gaussian * _AlphaScale;

                return float4(i.color.rgb, alpha);
            }

            ENDHLSL
        }
    }

}
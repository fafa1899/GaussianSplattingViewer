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
                float padding0;

                float3 scale;
                float padding1;

                float4 rotation; // xyzw
                float4 color;    // rgba
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
                float2 ellipseUV : TEXCOORD0;
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

            float3 RotateByQuaternion(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                GaussianData g = _Gaussians[instanceID];
                float2 corner = GetCornerUV(vertexID);

                float4 q = g.rotation;
                float qLen = length(q);
                if (qLen > 1e-6)
                {
                    q /= qLen;
                }
                else
                {
                    q = float4(0, 0, 0, 1);
                }

                // 先取局部两个主轴。当前先用 x/y 两个轴近似椭圆。
                float3 localAxisX = float3(g.scale.x, 0, 0);
                float3 localAxisY = float3(0, g.scale.y, 0);

                float3 worldAxisX = RotateByQuaternion(localAxisX, q);
                float3 worldAxisY = RotateByQuaternion(localAxisY, q);

                // 投影到相机平面基底（right/up）
                float2 projAxisX = float2(
                    dot(worldAxisX, _CameraRightWS),
                    dot(worldAxisX, _CameraUpWS)
                );

                float2 projAxisY = float2(
                    dot(worldAxisY, _CameraRightWS),
                    dot(worldAxisY, _CameraUpWS)
                );

                float2 inPlaneOffset =
                    projAxisX * corner.x +
                    projAxisY * corner.y;

                float3 centerWS = mul(_LocalToWorld, float4(g.position, 1.0)).xyz;
                float3 offsetWS =
                    _CameraRightWS * inPlaneOffset.x +
                    _CameraUpWS * inPlaneOffset.y;

                float3 positionWS = centerWS + offsetWS;

                o.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                o.ellipseUV = corner;
                o.color = saturate(g.color);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 当前用椭圆局部坐标做高斯衰减
                float r2 = dot(i.ellipseUV, i.ellipseUV);

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
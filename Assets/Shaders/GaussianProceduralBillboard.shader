Shader "GaussianSplatting/ProceduralBillboard"
{
	Properties
    {
        _AlphaScale ("Alpha Scale", Float) = 1.0
        _GaussianSharpness ("Gaussian Sharpness", Float) = 1.0
        _SigmaExtent ("Sigma Extent", Float) = 3.0
        _R2Cutoff ("R2 Cutoff", Float) = 9.0
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
            float _SigmaExtent;
            float _R2Cutoff;

            float4x4 _LocalToWorld;
            float3 _CameraRightWS;
            float3 _CameraUpWS;

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 planeOffset : TEXCOORD0;
                float3 invCov : TEXCOORD1; // (a, b, c) for [a b; b c]
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

            float2x2 Outer(float2 v)
            {
                return float2x2(
                    v.x * v.x, v.x * v.y,
                    v.y * v.x, v.y * v.y
                );
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

                // 三个局部主轴，带真实尺度
                float3 axisX = RotateByQuaternion(float3(g.scale.x, 0, 0), q);
                float3 axisY = RotateByQuaternion(float3(0, g.scale.y, 0), q);
                float3 axisZ = RotateByQuaternion(float3(0, 0, g.scale.z), q);

                // 投影到相机平面基底 right/up
                float2 pX = float2(dot(axisX, _CameraRightWS), dot(axisX, _CameraUpWS));
                float2 pY = float2(dot(axisY, _CameraRightWS), dot(axisY, _CameraUpWS));
                float2 pZ = float2(dot(axisZ, _CameraRightWS), dot(axisZ, _CameraUpWS));

                // 2D 协方差近似：Sigma = sum(axis_i * axis_i^T)
                float2x2 cov = Outer(pX) + Outer(pY) + Outer(pZ);

                // 数值稳定
                cov[0][0] += 1e-8;
                cov[1][1] += 1e-8;

                float a = cov[0][0];
                float b = cov[0][1];
                float c = cov[1][1];

                // 对称 2x2 矩阵特征分解
                float trace = a + c;
                float det = a * c - b * b;
                float disc = sqrt(max(trace * trace * 0.25 - det, 0.0));

                float lambda1 = max(trace * 0.5 + disc, 1e-8);
                float lambda2 = max(trace * 0.5 - disc, 1e-8);

                float2 dir1;
                if (abs(b) > 1e-6)
                {
                    dir1 = normalize(float2(b, lambda1 - a));
                }
                else
                {
                    dir1 = (a >= c) ? float2(1, 0) : float2(0, 1);
                }

                float2 dir2 = float2(-dir1.y, dir1.x);

                float extent1 = sqrt(lambda1) * _SigmaExtent;
                float extent2 = sqrt(lambda2) * _SigmaExtent;

                // quad 顶点在屏幕平面内的偏移（单位：camera plane world units）
                float2 planeOffset =
                    dir1 * (corner.x * extent1) +
                    dir2 * (corner.y * extent2);

                float3 centerWS = mul(_LocalToWorld, float4(g.position, 1.0)).xyz;
                float3 offsetWS =
                    _CameraRightWS * planeOffset.x +
                    _CameraUpWS * planeOffset.y;
                          
                float3 positionWS = centerWS + offsetWS;

                // 逆协方差，用于 fragment 里的二次型
                float invDet = 1.0 / max(det, 1e-8);
                float invA =  c * invDet;
                float invB = -b * invDet;
                float invC =  a * invDet;

                o.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                o.planeOffset = planeOffset;
                o.invCov = float3(invA, invB, invC);
                o.color = saturate(g.color);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float x = i.planeOffset.x;
                float y = i.planeOffset.y;

                // r2 = p^T * invCov * p
                float r2 =
                    i.invCov.x * x * x +
                    2.0 * i.invCov.y * x * y +
                    i.invCov.z * y * y;

                if (r2 > _R2Cutoff)
                {
                    discard;
                }

                float gaussian = exp(-0.5 * r2 * _GaussianSharpness);
                float alpha = i.color.a * gaussian * _AlphaScale;

                return float4(i.color.rgb, alpha);
            }

            ENDHLSL
        }
    }

}
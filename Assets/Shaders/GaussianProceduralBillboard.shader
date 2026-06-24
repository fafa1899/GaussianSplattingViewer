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

                float4 sh01;
                float4 sh02;
                float4 sh03;
                float4 sh04;
                float4 sh05;
                float4 sh06;
                float4 sh07;
                float4 sh08;
                float4 sh09;
                float4 sh10;
                float4 sh11;
                float4 sh12;
                float4 sh13;
                float4 sh14;
                float4 sh15;
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

                float3 centerWS : TEXCOORD2;

                float3 sh01 : TEXCOORD3;
                float3 sh02 : TEXCOORD4;
                float3 sh03 : TEXCOORD5;
                float3 sh04 : TEXCOORD6;
                float3 sh05 : TEXCOORD7;
                float3 sh06 : TEXCOORD8;
                float3 sh07 : TEXCOORD9;
                float3 sh08 : TEXCOORD10;
                float3 sh09 : TEXCOORD11;
                float3 sh10 : TEXCOORD12;
                float3 sh11 : TEXCOORD13;
                float3 sh12 : TEXCOORD14;
                float3 sh13 : TEXCOORD15;
                float3 sh14 : TEXCOORD16;
                float3 sh15 : TEXCOORD17;
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

            float3 EvaluateShColor(
                float3 baseColor,
                float3 sh01, float3 sh02, float3 sh03,
                float3 sh04, float3 sh05, float3 sh06, float3 sh07, float3 sh08,
                float3 sh09, float3 sh10, float3 sh11, float3 sh12, float3 sh13, float3 sh14, float3 sh15,
                float3 viewDirWS)
            {
                // 和官方 sh_utils.py / eval_sh 的 1 阶形式对应
                const float C1 = 0.4886025119029199;

                const float C2_0 = 1.0925484305920792;
                const float C2_1 = -1.0925484305920792;
                const float C2_2 = 0.31539156525252005;
                const float C2_3 = -1.0925484305920792;
                const float C2_4 = 0.5462742152960396;

                const float C3_0 = -0.5900435899266435;
                const float C3_1 = 2.890611442640554;
                const float C3_2 = -0.4570457994644658;
                const float C3_3 = 0.3731763325901154;
                const float C3_4 = -0.4570457994644658;
                const float C3_5 = 1.445305721320277;
                const float C3_6 = -0.5900435899266435;

                float x = viewDirWS.x;
                float y = viewDirWS.y;
                float z = viewDirWS.z;
                float xx = x * x;
                float yy = y * y;
                float zz = z * z;
                float xy = x * y;
                float yz = y * z;
                float xz = x * z;

                // baseColor 当前已经是 f_dc * C0 + 0.5             
                float3 color = baseColor;

                // l = 1
                color += (-C1 * y) * sh01;
                color += ( C1 * z) * sh02;
                color += (-C1 * x) * sh03;

                // l = 2
                color += (C2_0 * xy) * sh04;
                color += (C2_1 * yz) * sh05;
                color += (C2_2 * (2.0 * zz - xx - yy)) * sh06;
                color += (C2_3 * xz) * sh07;
                color += (C2_4 * (xx - yy)) * sh08;

                // l = 3
                color += (C3_0 * y * (3.0 * xx - yy)) * sh09;
                color += (C3_1 * xy * z) * sh10;
                color += (C3_2 * y * (4.0 * zz - xx - yy)) * sh11;
                color += (C3_3 * z * (2.0 * zz - 3.0 * xx - 3.0 * yy)) * sh12;
                color += (C3_4 * x * (4.0 * zz - xx - yy)) * sh13;
                color += (C3_5 * z * (xx - yy)) * sh14;
                color += (C3_6 * x * (xx - 3.0 * yy)) * sh15;

                return color;
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
                o.centerWS = centerWS;

                o.sh01 = g.sh01.xyz;
                o.sh02 = g.sh02.xyz;
                o.sh03 = g.sh03.xyz;
                o.sh04 = g.sh04.xyz;
                o.sh05 = g.sh05.xyz;
                o.sh06 = g.sh06.xyz;
                o.sh07 = g.sh07.xyz;
                o.sh08 = g.sh08.xyz;
                o.sh09 = g.sh09.xyz;
                o.sh10 = g.sh10.xyz;
                o.sh11 = g.sh11.xyz;
                o.sh12 = g.sh12.xyz;
                o.sh13 = g.sh13.xyz;
                o.sh14 = g.sh14.xyz;
                o.sh15 = g.sh15.xyz;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float x = i.planeOffset.x;
                float y = i.planeOffset.y;

                float r2 =
                    i.invCov.x * x * x +
                    2.0 * i.invCov.y * x * y +
                    i.invCov.z * y * y;

                if (r2 > _R2Cutoff)
                {
                    discard;
                }

                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - i.centerWS);

                float3 shColor = EvaluateShColor(
                    i.color.rgb,
                    i.sh01, i.sh02, i.sh03,
                    i.sh04, i.sh05, i.sh06, i.sh07, i.sh08,
                    i.sh09, i.sh10, i.sh11, i.sh12, i.sh13, i.sh14, i.sh15,
                    viewDirWS
                );

                float gaussian = exp(-0.5 * r2 * _GaussianSharpness);
                float alpha = i.color.a * gaussian * _AlphaScale;

                return float4(saturate(shColor), alpha);
            }

            ENDHLSL
        }
    }

}
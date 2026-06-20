Shader "GaussianSplatting/Billboard"
{
	Properties
	{
		_SplatRadius ("Splat Radius", Float) = 0.01
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
            Cull Off
            ZTest LEqual

			HLSLPROGRAM

			#pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

			#include "UnityCG.cginc"

			float _SplatRadius;
            float _AlphaScale;
            float _GaussianSharpness;
            float3 _CameraRightWS;
            float3 _CameraUpWS;

			struct appdata
            {
                float3 centerOS : POSITION;
                float2 quadUV : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 quadUV : TEXCOORD0;
                float4 color : COLOR;
            };


            v2f vert(appdata v)
            {
                v2f o;

                float3 centerWS = mul(unity_ObjectToWorld, float4(v.centerOS, 1.0)).xyz;
                float3 offsetWS =
                    _CameraRightWS * (v.quadUV.x * _SplatRadius) +
                    _CameraUpWS * (v.quadUV.y * _SplatRadius);

                float3 positionWS = centerWS + offsetWS;

                o.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                o.quadUV = v.quadUV;
                o.color = saturate(v.color);

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

Shader "GaussianSplatting/PointCloud"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {   
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            float _PointSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float pointSize : PSIZE;
            };


            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.color = saturate(v.color);
                o.pointSize = _PointSize;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }

            ENDHLSL
        }
    }
}
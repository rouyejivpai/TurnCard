// 卡牌「可打出」的柔和光边。
// 不用贴图：按 UV 到矩形边缘的距离算出一圈内发光，
// 所以任何尺寸都能用，也不需要额外美术素材。
Shader "TurnCard/UIEdgeGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Glow Color", Color) = (1,1,1,0.55)
        _Width ("Glow Width (0-0.5)", Range(0, 0.5)) = 0.055
        _Softness ("Softness", Range(0.001, 0.5)) = 0.03
        _CornerRadius ("Corner Radius (0-0.5)", Range(0, 0.5)) = 0.06
        _InnerCut ("Inner Cutout", Range(0, 1)) = 0.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Width;
            float _Softness;
            float _CornerRadius;
            float _InnerCut;
            float4 _ClipRect;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 到最近边的距离（0 = 边缘，0.5 = 中心）
                float e = min(min(i.texcoord.x, 1.0 - i.texcoord.x),
                              min(i.texcoord.y, 1.0 - i.texcoord.y));

                // 边缘最亮，向内快速衰减
                float a = 1.0 - smoothstep(0.0, _Width, e);
                a = pow(saturate(a), 1.6) * _Color.a;

                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}

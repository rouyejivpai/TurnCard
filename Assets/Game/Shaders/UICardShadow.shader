// 卡牌悬停时的「悬浮投影」：把卡面贴图的 alpha 当剪影，染成半透明黑，
// 偏移 + 柔化后画在卡牌背后。不用额外的影子贴图，卡框换了影子自动跟着变。
Shader "TurnCard/UICardShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Shadow Color", Color) = (0, 0, 0, 0.42)
        _Offset ("Offset (uv)", Vector) = (0.03, -0.045, 0, 0)
        _Blur ("Blur (uv)", Range(0, 0.1)) = 0.012
        _Spread ("Spread", Range(0.8, 1.4)) = 1.04

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
        Blend SrcAlpha OneMinusSrcAlpha
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
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Offset;
            float _Blur;
            float _Spread;
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

            // 9 点取样：中心 + 4 正向 + 4 斜向，做廉价柔化
            float SampleAlpha(float2 uv, float2 d)
            {
                float a = tex2D(_MainTex, uv).a;
                a += tex2D(_MainTex, uv + float2(d.x, 0)).a;
                a += tex2D(_MainTex, uv - float2(d.x, 0)).a;
                a += tex2D(_MainTex, uv + float2(0, d.y)).a;
                a += tex2D(_MainTex, uv - float2(0, d.y)).a;
                a += tex2D(_MainTex, uv + d).a;
                a += tex2D(_MainTex, uv - d).a;
                a += tex2D(_MainTex, uv + float2(d.x, -d.y)).a;
                a += tex2D(_MainTex, uv - float2(d.x, -d.y)).a;
                return a / 9.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 稍微外扩一点，影子比卡牌大一圈
                float2 uv = (i.texcoord - 0.5) / max(_Spread, 1e-3) + 0.5;
                uv += _Offset.xy;

                float a = SampleAlpha(uv, float2(_Blur, _Blur));
                a *= _Color.a * i.color.a * UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return fixed4(_Color.rgb * i.color.rgb, a);
            }
            ENDCG
        }
    }
}

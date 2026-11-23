// 卡牌「可打出」的描边：按**卡框贴图的 alpha 轮廓**向外膨胀出一圈，
// 而不是按矩形 UV 到边缘的距离——所以描边会贴着卡框真实的形状（圆角、缺口都跟）。
//
// 用法：一张比卡牌大一圈的 Image（垫在卡牌下面），sprite 用纯白方块，
// 通过 _ShapeTex 传入卡框贴图，_Expand 表示「卡牌在这张图里占多大」。
Shader "TurnCard/UICardOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Quad Texture", 2D) = "white" {}
        _ShapeTex ("Card Shape", 2D) = "white" {}

        _Color ("Outline Color", Color) = (1, 0.84, 0.35, 1)
        _Width ("Outline Width (uv)", Range(0.001, 0.25)) = 0.035
        _Expand ("Card Fill (uv)", Range(1, 2)) = 1.14
        _Intensity ("Intensity", Range(0, 4)) = 1.2

        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.22
        _PulseSpeed ("Pulse Speed", Float) = 2.6

        _Softness ("Softness", Range(0, 1)) = 0.55

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
            #pragma target 3.0

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
            sampler2D _ShapeTex;
            fixed4 _Color;
            float _Width;
            float _Expand;
            float _Intensity;
            float _PulseAmount;
            float _PulseSpeed;
            float _Softness;
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

            // 卡牌在贴图里的 alpha；贴图外一律当透明
            float ShapeAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                {
                    return 0;
                }

                return tex2D(_ShapeTex, uv).a;
            }

            // 一圈 8 方向的最大 alpha = 膨胀
            float RingMax(float2 uv, float radius)
            {
                float m = 0;
                float2 d = float2(radius, 0);
                m = max(m, ShapeAlpha(uv + d));
                m = max(m, ShapeAlpha(uv + d * 0.7071 + float2(0, radius * 0.7071)));
                m = max(m, ShapeAlpha(uv + float2(0, radius)));
                m = max(m, ShapeAlpha(uv - d * 0.7071 + float2(0, radius * 0.7071)));
                m = max(m, ShapeAlpha(uv - d));
                m = max(m, ShapeAlpha(uv - d * 0.7071 - float2(0, radius * 0.7071)));
                m = max(m, ShapeAlpha(uv - float2(0, radius)));
                m = max(m, ShapeAlpha(uv + d * 0.7071 - float2(0, radius * 0.7071)));
                return m;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 光晕图比卡牌大一圈，先把 uv 映射回「卡牌本体」的坐标
                float2 uv = (i.texcoord - 0.5) / _Expand + 0.5;

                float center = ShapeAlpha(uv);

                // 只在「紧贴轮廓」的一圈内画：半径就是描边宽度，
                // 不再往外叠柔光，否则会糊成一圈很大的光晕、看起来离卡牌很远。
                float near = RingMax(uv, _Width);
                float a = saturate(near - center);

                // 柔化：只是把边缘 gamma 一下，不改变描边厚度
                a = lerp(a, a * a, _Softness);

                // 呼吸：整体亮度轻微起伏，静止时也能看出「可打出」
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                a *= _Color.a * _Intensity * pulse;
                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}

// 敌人卡死亡：刀痕斜切 → 上下两半分离 → 消散。
//
// 一个 quad 无法分成两块朝不同方向移动，所以由 C# 复制出两套 Image（上半 / 下半），
// 各自使用一个材质实例，只靠 _Half 区分保留哪一侧。切分线、位移、旋转、溶解都在这里完成，
// 美术只需要一张「刀光」贴图（可选）。
Shader "TurnCard/UICardSlashDeath"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Slash)]
        _Half ("Half (0=上, 1=下)", Float) = 0
        _CutAngle ("Cut Angle (deg)", Float) = -28
        _CutOffset ("Cut Offset", Float) = 0
        _EdgeWidth ("Edge Width", Range(0.001, 0.3)) = 0.05
        _EdgeColor ("Edge Color", Color) = (1, 1, 0.86, 1)
        _EdgePower ("Edge Power", Range(0, 4)) = 1.6

        [Header(Split)]
        _HalfOffset ("Half Offset (px)", Vector) = (0, 0, 0, 0)
        _HalfRotation ("Half Rotation (deg)", Float) = 0

        [Header(Dissolve)]
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveScale ("Dissolve Scale", Float) = 14
        _DissolveSoftness ("Dissolve Softness", Range(0.001, 0.5)) = 0.08

        [Header(Alpha)]
        _Alpha ("Alpha", Range(0, 1)) = 1

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
                float2 localPos : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _Half;
            float _CutAngle;
            float _CutOffset;
            float _EdgeWidth;
            float4 _EdgeColor;
            float _EdgePower;
            float4 _HalfOffset;
            float _HalfRotation;
            float _Dissolve;
            float _DissolveScale;
            float _DissolveSoftness;
            float _Alpha;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // 绕卡片中心（本地原点，pivot = 0.5/0.5）旋转这一半，再整体平移
                float rad = radians(_HalfRotation);
                float s = sin(rad);
                float c = cos(rad);
                float2 p = v.vertex.xy;
                float2 rotated = float2(p.x * c - p.y * s, p.x * s + p.y * c);

                float4 pos = float4(rotated + _HalfOffset.xy, v.vertex.z, 1.0);
                o.worldPosition = pos;
                o.vertex = UnityObjectToClipPos(pos);

                o.texcoord = v.texcoord;
                o.localPos = v.texcoord - 0.5;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1) 到切分线的有符号距离
                float rad = radians(_CutAngle);
                float2 dir = float2(cos(rad), sin(rad));
                float d = dot(i.localPos, dir) - _CutOffset;

                // 2) 只保留自己那一侧
                if (_Half < 0.5)
                {
                    if (d < 0) discard;
                }
                else
                {
                    if (d >= 0) discard;
                }

                // 3) 刀痕亮边
                float edge = saturate(1.0 - abs(d) / max(_EdgeWidth, 1e-4));
                edge = pow(edge, _EdgePower);

                // 4) 溶解（程序噪声，不需要溶解图）
                float n = frac(sin(dot(i.texcoord, float2(12.9898, 78.233))) * 43758.5453);
                float threshold = _Dissolve;
                if (n < threshold) discard;

                // 溶解边缘再亮一圈，读起来像被烧掉
                float burning = saturate(1.0 - abs(n - threshold) / max(_DissolveSoftness, 1e-4));

                fixed4 tex = tex2D(_MainTex, i.texcoord) * i.color;

                fixed3 rgb = lerp(tex.rgb, _EdgeColor.rgb, saturate(edge + burning * 0.75));
                float alpha = tex.a * _Alpha;

                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}

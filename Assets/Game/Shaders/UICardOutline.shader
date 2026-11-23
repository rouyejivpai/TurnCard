// 卡牌「可打出」的描边，**直接画在 Background 这张图上**：
// Background 的贴图自带透明边距（cardframeWood 左 8 / 上 4 / 右 10 / 下 10 贴图像素），
// 描边就画在那圈透明边距里，所以不需要额外再垫一张大一圈的图。
//
// 同一份材质还会画卡面本身（贴图 × 顶点色），所以可以直接替换 Background 的材质，
// 不播放边时把材质换回默认 UI 材质即可。
Shader "TurnCard/UICardOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Outline)]
        _Color ("Inner Color (贴边)", Color) = (1, 0.78, 0.25, 1)
        _PaleColor ("Outer Color (外沿)", Color) = (1, 0.95, 0.75, 1)
        _Width ("Outline Width (uv)", Range(0.001, 0.25)) = 0.021
        _Intensity ("Intensity", Range(0, 4)) = 1.0
        _Softness ("Softness", Range(0, 1)) = 0.3
        _OuterAlpha ("Outer Alpha", Range(0, 1)) = 0.35

        [Header(Wave)]
        _WaveColor ("Wave Color", Color) = (1, 1, 0.92, 1)
        _WaveStrength ("Wave Strength", Range(0, 2)) = 0.55
        _WaveSpeed ("Wave Speed", Float) = 1.1
        _WaveBand ("Wave Band (0-1 of width)", Range(0.02, 1)) = 0.35

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
            fixed4 _Color;
            fixed4 _PaleColor;
            float _Width;
            float _Intensity;
            float _Softness;
            float _OuterAlpha;
            fixed4 _WaveColor;
            float _WaveStrength;
            float _WaveSpeed;
            float _WaveStart;
            float _WaveEnd;
            float _WaveBand;
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

            float ShapeAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                {
                    return 0;
                }

                return tex2D(_MainTex, uv).a;
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

            // 到卡牌轮廓的近似距离：沿 1/6~6/6 倍宽度分档探测。
            // 单半径采样会漏掉落在两档之间的像素（描边出现断裂），所以必须分档；
            // 档位越密，内深外浅的渐变越连续。
            float DistanceToShape(float2 uv, float maxR)
            {
                float stepR = maxR / 6.0;
                if (RingMax(uv, stepR) > 0.5) return stepR;
                if (RingMax(uv, stepR * 2.0) > 0.5) return stepR * 2.0;
                if (RingMax(uv, stepR * 3.0) > 0.5) return stepR * 3.0;
                if (RingMax(uv, stepR * 4.0) > 0.5) return stepR * 4.0;
                if (RingMax(uv, stepR * 5.0) > 0.5) return stepR * 5.0;
                if (RingMax(uv, stepR * 6.0) > 0.5) return stepR * 6.0;
                return maxR * 1.01;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 face = tex2D(_MainTex, i.texcoord) * i.color;

                float center = ShapeAlpha(i.texcoord);
                float d = DistanceToShape(i.texcoord, _Width);

                // t：0 = 贴着卡牌，1 = 描边最外沿
                float t = saturate(d / max(_Width, 1e-4));

                // 内深外浅：颜色从饱和色渐变到浅色，不透明度同步下降
                float3 outlineRgb = lerp(_Color.rgb, _PaleColor.rgb, t);
                float outlineA = (1.0 - t) * lerp(1.0, _OuterAlpha, t);

                // 柔化只压边缘，不改厚度
                outlineA = lerp(outlineA, outlineA * outlineA, _Softness);

                // 卡牌本体内部不画描边
                outlineA *= (1.0 - center);

                // ---- 由内向外扩散的波：用同一个 t，所以不会超出描边范围 ----
                float phase = frac(_Time.y * _WaveSpeed);
                float band = max(_WaveBand, 0.02);
                float wave = saturate(1.0 - abs(t - phase) / band);
                wave = wave * wave * (1.0 - phase) * _WaveStrength;

                float ring = saturate(outlineA + wave);
                float3 ringRgb = lerp(outlineRgb, _WaveColor.rgb, wave / max(ring, 1e-4));
                ring = saturate(ring * _Intensity);

                float a = saturate(face.a + ring);
                float3 rgb = (face.rgb * face.a + ringRgb * ring) / max(a, 1e-4);

                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}

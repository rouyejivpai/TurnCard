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
        _Color ("Outline Color", Color) = (1, 0.84, 0.35, 1)
        _Width ("Outline Width (uv)", Range(0.001, 0.25)) = 0.021
        _Intensity ("Intensity", Range(0, 4)) = 1.0

        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.22
        _PulseSpeed ("Pulse Speed", Float) = 2.6

        _Softness ("Softness", Range(0, 1)) = 0.3

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
            float _Width;
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

            float ShapeAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                {
                    return 0;
                }

                return tex2D(_MainTex, uv).a;
            }

            // 一圈 8 方向的最大 alpha = 膨胀；半径就是描边厚度
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
                fixed4 face = tex2D(_MainTex, i.texcoord) * i.color;

                // 只在「紧贴轮廓外侧」的一圈里画描边
                float center = ShapeAlpha(i.texcoord);
                float near = RingMax(i.texcoord, _Width);
                float ring = saturate(near - center);

                // 柔化只影响边缘软硬，不改变厚度
                ring = lerp(ring, ring * ring, _Softness);

                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                ring = saturate(ring * _Color.a * _Intensity * pulse);

                // 把卡面和描边按 alpha 合成，返回非预乘颜色（配合 SrcAlpha OneMinusSrcAlpha）
                float a = saturate(face.a + ring);
                float3 rgb = (face.rgb * face.a + _Color.rgb * ring) / max(a, 1e-4);

                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}

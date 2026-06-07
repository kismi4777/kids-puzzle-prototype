Shader "Puzzle/BWMaskShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("BW Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _EdgeSoftness ("Edge Softness", Float) = 0.5
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1
        _HoleCount ("Hole Count", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BWMaskForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _EdgeSoftness;
                float _GlobalAlpha;
                int _HoleCount;
                float4 _Holes[10];
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = worldPosition;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            // Маска дырок: 0 внутри радиуса (прозрачно), 1 снаружи (виден ЧБ-слой).
            float ComputeHoleMask(float2 worldXZ)
            {
                float mask = 1.0;
                int count = clamp(_HoleCount, 0, 10);

                for (int i = 0; i < count; i++)
                {
                    float2 holeCenter = _Holes[i].xy;
                    float radius = _Holes[i].w;
                    float dist = distance(worldXZ, holeCenter);

                    // smoothstep: внутри radius -> 0, за пределами radius+softness -> 1.
                    float holeVisibility = smoothstep(radius, radius + _EdgeSoftness, dist);
                    mask = min(mask, holeVisibility);
                }

                return mask;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float holeMask = ComputeHoleMask(input.worldPos.xz);
                texColor.a *= holeMask * _GlobalAlpha;
                return texColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

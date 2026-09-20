Shader "CardsUnity/Story Veil Dissolve"
{
    Properties
    {
        [MainColor] _BaseColor ("Veil Color", Color) = (0.008, 0.006, 0.009, 1)
        _Progress ("Dissolve Progress", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Range(0.1, 40)) = 5
        _EdgeWidth ("Warm Edge Width", Range(0.001, 0.25)) = 0.055
        _EdgeColor ("Warm Edge Color", Color) = (0.38, 0.09, 0.025, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "StoryVeil"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Progress;
                float _NoiseScale;
                float _EdgeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 fraction = frac(p);
                float2 blend = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), blend.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + float2(1, 1)), blend.x), blend.y);
            }

            float OrganicNoise(float2 uv)
            {
                float coarse = ValueNoise(uv);
                float medium = ValueNoise(uv * 2.07 + 19.1);
                float detail = ValueNoise(uv * 4.13 - 7.4);
                return saturate(coarse * 0.62 + medium * 0.28 + detail * 0.10);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float progress = saturate(_Progress);
                float noise = OrganicNoise(input.uv * _NoiseScale) * 0.999;
                // At zero the veil remains solid; at one every fragment is clipped.
                float dissolve = progress <= 0.00001 ? 1.0 : noise - progress;
                clip(dissolve);

                float edge = progress <= 0.00001 ? 0.0 : 1.0 - smoothstep(0.0, max(_EdgeWidth, 0.0001), dissolve);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, edge);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

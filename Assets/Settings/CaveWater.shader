Shader "Cards Unity/Cave Water"
{
    Properties
    {
        _BaseColor("Water color", Color) = (0.18, 0.88, 1.0, 1.0)
        _Opacity("Opacity", Range(0, 1)) = 0.66
        _FlowSpeed("Flow speed", Range(0, 4)) = 1.25
        _StreakScale("Streak scale", Range(1, 20)) = 8
        _RippleScale("Ripple scale", Range(1, 20)) = 7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CaveWaterUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                float _FlowSpeed;
                float _StreakScale;
                float _RippleScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 metadata : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float pool : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.pool = input.metadata.x;
                return output;
            }
            float Hash(float value) { return frac(sin(value * 91.739) * 43758.5453); }
            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                float stripe = input.uv.x * _StreakScale;
                float column = floor(stripe);
                // UV y is zero at the pool and one at the source; increasing time therefore sends streaks down.
                float drift = frac(input.uv.y + time - Hash(column) * 0.8);
                float stream = smoothstep(0.16, 0.5, sin(stripe * 6.28318 + Hash(column) * 6.28318) * 0.5 + 0.5);
                float falling = stream * (0.55 + 0.45 * sin(drift * 12.56636));
                float2 centered = input.uv - 0.5;
                float radius = length(centered) * 2.0;
                float ring = 0.5 + 0.5 * sin(radius * _RippleScale * 6.28318 - time * 5.0);
                float pool = saturate((1.0 - radius) * (0.3 + 0.7 * ring));
                float brightness = lerp(falling, pool, input.pool);
                half3 color = _BaseColor.rgb * (0.62h + 0.72h * brightness);
                half alpha = _Opacity * lerp(0.38h + 0.55h * falling, pool * (0.2h + 0.5h * ring), input.pool);
                return half4(color, alpha);
            }
            ENDHLSL
        }
        // This pass feeds the dither accent mask. The renderer feature draws transparent accent passes separately.
        Pass
        {
            Name "DitherAccent"
            Tags { "LightMode"="DitherAccent" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex AccentVert
            #pragma fragment AccentFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
                float _FlowSpeed;
                float _StreakScale;
                float _RippleScale;
            CBUFFER_END

            struct AccentAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 metadata : TEXCOORD1;
            };
            struct AccentVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float pool : TEXCOORD1;
            };
            AccentVaryings AccentVert(AccentAttributes input)
            {
                AccentVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.pool = input.metadata.x;
                return output;
            }
            float AccentHash(float value) { return frac(sin(value * 91.739) * 43758.5453); }
            half4 AccentFrag(AccentVaryings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                float stripe = input.uv.x * _StreakScale;
                float column = floor(stripe);
                float drift = frac(input.uv.y + time - AccentHash(column) * 0.8);
                float stream = smoothstep(0.16, 0.5, sin(stripe * 6.28318 + AccentHash(column) * 6.28318) * 0.5 + 0.5);
                float falling = stream * (0.55 + 0.45 * sin(drift * 12.56636));
                float2 centered = input.uv - 0.5;
                float radius = length(centered) * 2.0;
                float ring = 0.5 + 0.5 * sin(radius * _RippleScale * 6.28318 - time * 5.0);
                float pool = saturate((1.0 - radius) * (0.3 + 0.7 * ring));
                float brightness = lerp(falling, pool, input.pool);
                half3 color = _BaseColor.rgb * (0.62h + 0.72h * brightness);
                half alpha = _Opacity * lerp(0.38h + 0.55h * falling, pool * (0.2h + 0.5h * ring), input.pool);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

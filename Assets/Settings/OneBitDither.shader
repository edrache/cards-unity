Shader "CardsUnity/One Bit Dither"
{
    Properties
    {
        _Ink ("Ink", Color) = (0.025, 0.035, 0.03, 1)
        _Paper ("Paper", Color) = (0.88, 0.85, 0.72, 1)
        _PixelSize ("Pixel Size", Range(1, 8)) = 2
        _Exposure ("Exposure", Range(0.25, 4)) = 3.5
        _Contrast ("Contrast", Range(0.5, 3)) = 1
        _DitherStrength ("Dither Strength", Range(0, 1)) = 1
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "One Bit Dither"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Ink, _Paper;
                float _PixelSize, _Exposure, _Contrast, _DitherStrength, _EdgeStrength;
            CBUFFER_END

            float Luma(float2 uv)
            {
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                // Threshold in perceptual space so dark torch gradients stay readable.
                return dot(LinearToSRGB(max(color, 0)), float3(0.2126, 0.7152, 0.0722));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float pixelSize = max(1, round(_PixelSize));
                float2 size = _BlitTexture_TexelSize.zw;
                float2 pixel = floor(input.texcoord * size / pixelSize);
                float2 uv = (pixel + 0.5) * pixelSize / size;
                float2 stepUV = pixelSize / size;
                float luminance = Luma(uv);
                float edge = max(abs(luminance - Luma(uv + float2(stepUV.x, 0))),
                                 abs(luminance - Luma(uv + float2(0, stepUV.y))));
                luminance = saturate((luminance * _Exposure - 0.5) * _Contrast + 0.5 - edge * _EdgeStrength);

                // A fixed Bayer matrix avoids temporal noise and preserves exact two-color output.
                const float bayer[16] = {
                    0, 8, 2, 10,
                    12, 4, 14, 6,
                    3, 11, 1, 9,
                    15, 7, 13, 5
                };
                uint2 cell = (uint2)pixel & 3;
                float threshold = lerp(0.5, (bayer[cell.y * 4 + cell.x] + 0.5) / 16.0, _DitherStrength);
                return float4(lerp(_Ink.rgb, _Paper.rgb, step(threshold, luminance)), 1);
            }
            ENDHLSL
        }
    }
}

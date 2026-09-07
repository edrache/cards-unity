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
        [Enum(Bayer, 0, Noise Texture, 1)] _DitherMode ("Dither Mode", Float) = 0
        [NoScaleOffset] _NoiseTexture ("Noise Texture (Grayscale)", 2D) = "gray" {}
        _NoiseTileSize ("Noise Tile Size (Dither Pixels)", Range(4, 1024)) = 256
        _EdgeStrength ("Edge Strength", Range(0, 2)) = 0.6
        [IntRange] _ToneCount ("Tone Count", Range(2, 16)) = 2
        _PaperGrain ("Paper Grain Strength", Range(0, 0.25)) = 0
        _BoundaryStrength ("Light Boundary Irregularity", Range(0, 1)) = 0
        _BoundaryScale ("Light Boundary Size (Dither Pixels)", Range(4, 256)) = 32
        [NoScaleOffset] _PaperTexture ("Paper Texture (Color Dodge)", 2D) = "black" {}
        _PaperTileSize ("Paper Tile Size (Screen Pixels)", Range(64, 4096)) = 1024
        _PaperDodgeStrength ("Paper Color Dodge Strength", Range(0, 1)) = 0
        _PaperHighlightInfluence ("Paper Influence In Light", Range(0, 1)) = 0.1
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
                float _DitherMode, _NoiseTileSize;
                float _ToneCount, _PaperGrain, _BoundaryStrength, _BoundaryScale;
                float _PaperTileSize, _PaperDodgeStrength, _PaperHighlightInfluence;
                float4 _PaperTexture_TexelSize;
            CBUFFER_END
            TEXTURE2D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_PaperTexture);
            SAMPLER(sampler_PaperTexture);

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float PaperNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

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

                // Smooth, stationary noise bends tonal boundaries without displacing geometry.
                float2 boundaryUV = (pixel + 0.5) / max(1.0, _BoundaryScale);
                float boundary = PaperNoise(boundaryUV) * 0.7 + PaperNoise(boundaryUV * 2.13) * 0.3;
                float midtoneMask = 4.0 * luminance * (1.0 - luminance);
                luminance = saturate(luminance + (boundary - 0.5) * _BoundaryStrength * midtoneMask);

                // A fixed Bayer matrix avoids temporal noise.
                const float bayer[16] = {
                    0, 8, 2, 10,
                    12, 4, 14, 6,
                    3, 11, 1, 9,
                    15, 7, 13, 5
                };
                uint2 cell = (uint2)pixel & 3;
                float pattern = (bayer[cell.y * 4 + cell.x] + 0.5) / 16.0;
                if (_DitherMode > 0.5)
                {
                    // Anchor to the same screen pixel grid as Bayer; never animate the noise.
                    float2 noiseUV = frac((pixel + 0.5) / max(1.0, _NoiseTileSize));
                    pattern = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture, noiseUV, 0).r;
                    // Keep black and white source pixels solid even with 0/1 noise texels.
                    pattern = clamp(pattern, 0.5 / 255.0, 1.0 - 0.5 / 255.0);
                }
                float threshold = lerp(0.5, pattern, _DitherStrength);
                float intervals = max(1.0, round(_ToneCount) - 1.0);
                float scaled = luminance * intervals;
                float tone = min(intervals, floor(scaled) + step(threshold, frac(scaled))) / intervals;
                // Grain adds subtle continuous variation, including on otherwise blank paper.
                float2 grainUV = frac((pixel + 0.5) / max(1.0, _NoiseTileSize));
                float grain = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture, grainUV, 0).r;
                float3 color = lerp(_Ink.rgb, _Paper.rgb, tone);
                color *= 1.0 - _PaperGrain * (1.0 - grain);
                // Paper has its own screen-space scale, independent of the dither pixel size.
                // Preserve the source aspect ratio; the tile size specifies its width.
                float2 paperUV = input.texcoord * size / max(1.0, _PaperTileSize);
                paperUV.y *= _PaperTexture_TexelSize.z / max(1.0, _PaperTexture_TexelSize.w);
                float3 paperSample = SAMPLE_TEXTURE2D(_PaperTexture, sampler_PaperTexture, paperUV).rgb;
                // Blend in perceptual space, using monochrome paper to preserve the palette.
                float paperValue = dot(LinearToSRGB(paperSample), float3(0.2126, 0.7152, 0.0722));
                float3 baseValue = LinearToSRGB(max(color, 0));
                float3 dodge = saturate(baseValue / max(1.0 - paperValue, 0.001));
                // Smooth lighting mask prevents paper opacity stepping at quantized tone bands.
                float paperMask = lerp(1.0, _PaperHighlightInfluence, smoothstep(0.0, 1.0, luminance));
                color = lerp(color, SRGBToLinear(dodge), _PaperDodgeStrength * paperMask);
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}

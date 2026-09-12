Shader "CardsUnity/Cave Crawlers Lit"
{
    Properties
    {
        [MainColor] _BaseColor ("Rock Color", Color) = (0.17, 0.20, 0.24, 1)
        [MainTexture] _BaseMap ("Rock Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15

        [Header(Crawlers)]
        _CrawlerColor ("Crawler Color", Color) = (0.015, 0.008, 0.004, 1)
        _CrawlerDensity ("Cells Per Metre", Range(0.2, 2)) = 0.75
        _CrawlerPopulation ("Occupied Cells", Range(0, 1)) = 0.7
        _CrawlerSize ("Crawler Length (Metres)", Range(0.04, 0.5)) = 0.18
        _CrawlerSpeed ("Movement Speed", Range(0, 0.5)) = 0.075
        _CrawlerWiggle ("Path Wiggle (Metres)", Range(0, 0.3)) = 0.08
        _CrawlerSeed ("Seed", Range(0, 100)) = 17
        _CrawlerMaxDistance ("Maximum Visible Distance", Range(2, 40)) = 16
        _CrawlerFadeRange ("Distance Fade Range", Range(0.5, 10)) = 4
        _CrawlerWallThreshold ("Wall Only Threshold", Range(0, 0.95)) = 0.55

        [HideInInspector] _WorkflowMode ("Workflow", Float) = 1
        [HideInInspector] _SpecColor ("Specular", Color) = (0.2, 0.2, 0.2, 1)
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _BumpMap ("Normal", 2D) = "bump" {}
        [HideInInspector] _BumpScale ("Normal Scale", Float) = 1
        [HideInInspector] _OcclusionMap ("Occlusion", 2D) = "white" {}
        [HideInInspector] _OcclusionStrength ("Occlusion Strength", Float) = 1
        [HideInInspector] _EmissionMap ("Emission", 2D) = "white" {}
        [HideInInspector] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [HideInInspector] _Surface ("Surface", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("Source Alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha ("Destination Alpha", Float) = 0
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
        [HideInInspector] _AlphaToMask ("Alpha To Mask", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    float4 _CrawlerColor;
    float _CrawlerDensity;
    float _CrawlerPopulation;
    float _CrawlerSize;
    float _CrawlerSpeed;
    float _CrawlerWiggle;
    float _CrawlerSeed;
    float _CrawlerMaxDistance;
    float _CrawlerFadeRange;
    float _CrawlerWallThreshold;

    float CrawlerHash21(float2 value)
    {
        float3 p = frac(float3(value.x, value.y, value.x) * 0.1031);
        p += dot(p, p.yzx + 33.33);
        return frac((p.x + p.y) * p.z);
    }

    float CrawlerCapsuleDistance(float2 samplePosition, float2 segmentStart, float2 segmentFinish, float radius)
    {
        float2 segment = segmentFinish - segmentStart;
        float projection = saturate(dot(samplePosition - segmentStart, segment) / max(dot(segment, segment), 0.00001));
        return length(samplePosition - segmentStart - segment * projection) - radius;
    }

    float2 CrawlerWallCoordinates(float3 positionWS, half3 normalWS)
    {
        // Select the dominant horizontal normal so the second coordinate is always world height.
        // This avoids the stretched UVs produced by the procedural wall mesh.
        float useXProjection = step(abs(normalWS.z), abs(normalWS.x));
        return lerp(positionWS.xy, positionWS.zy, useXProjection);
    }

    float CrawlerShapeMask(float2 surfacePosition)
    {
        const float twoPi = 6.28318530718;
        float density = max(_CrawlerDensity, 0.001);
        float2 gridPosition = surfacePosition * density;
        float2 cell = floor(gridPosition);
        float2 localPosition = (frac(gridPosition) - 0.5) / density;

        float seed = CrawlerHash21(cell + _CrawlerSeed);
        float occupied = step(seed, _CrawlerPopulation);
        float directionSeed = CrawlerHash21(cell + _CrawlerSeed + 19.17);
        float speedSeed = CrawlerHash21(cell + _CrawlerSeed + 47.53);
        float phaseSeed = CrawlerHash21(cell + _CrawlerSeed + 83.91);

        float angle = directionSeed * twoPi;
        float2 direction = float2(cos(angle), sin(angle));
        float2 perpendicular = float2(-direction.y, direction.x);
        float phase = frac(_Time.y * _CrawlerSpeed * lerp(0.7, 1.3, speedSeed) + phaseSeed);
        float cellSize = rcp(density);
        // Keep the crawler inside its cell for the whole cycle. The previous one-way traversal
        // spent most of its time beyond the cell edge, making sparse populations appear absent.
        float travel = sin(phase * twoPi) * cellSize * 0.34;
        float pathOffset = cos(phase * twoPi + seed * 11.0) * _CrawlerWiggle;
        float2 centre = direction * travel + perpendicular * pathOffset;

        float2 delta = localPosition - centre;
        float2 crawler = float2(dot(delta, direction), dot(delta, perpendicular));
        float size = max(_CrawlerSize, 0.001);
        crawler.y += sin(crawler.x * 24.0 / size + phase * twoPi * 2.0) * _CrawlerWiggle * 0.12;

        float body = CrawlerCapsuleDistance(crawler, float2(-size * 0.38, 0),
            float2(size * 0.34, 0), size * 0.16);
        float head = length(crawler - float2(size * 0.48, 0)) - size * 0.22;

        // Cheap angular leg strokes make the capsule read as an insect after dithering.
        float legLine = abs(abs(crawler.y) - (size * 0.22 + abs(crawler.x) * 0.5)) - size * 0.035;
        float legBounds = max(abs(crawler.x) - size * 0.31, abs(crawler.y) - size * 0.46);
        float legs = max(legLine, legBounds);
        float signedDistance = min(min(body, head), legs);
        float antialiasing = max(fwidth(signedDistance), 0.0005);
        return occupied * (1.0 - smoothstep(-antialiasing, antialiasing, signedDistance));
    }

    float CrawlerSurfaceMask(float3 positionWS, half3 normalWS)
    {
        normalWS = normalize(normalWS);
        float wallFacing = 1.0 - abs(normalWS.y);
        float wallMask = smoothstep(_CrawlerWallThreshold,
            min(1.0, _CrawlerWallThreshold + 0.2), wallFacing);
        float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos);
        float fadeStart = max(0.0, _CrawlerMaxDistance - _CrawlerFadeRange);
        float distanceMask = 1.0 - smoothstep(fadeStart, _CrawlerMaxDistance, distanceToCamera);
        return CrawlerShapeMask(CrawlerWallCoordinates(positionWS, normalWS)) * wallMask * distanceMask;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        // Apply the crawler overlay directly in matching Lit passes for both URP rendering paths,
        // so it is also visible in the Material Inspector without a separate accent draw.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment CrawlerLitPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #define LitPassFragment BaseLitPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #undef LitPassFragment

            void CrawlerLitPassFragment(
                Varyings input,
                out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                BaseLitPassFragment(input, outColor
                #ifdef _WRITE_RENDERING_LAYERS
                    , outRenderingLayers
                #endif
                );

                float mask = CrawlerSurfaceMask(input.positionWS, input.normalWS);
                outColor.rgb = lerp(outColor.rgb, _CrawlerColor.rgb, saturate(mask * _CrawlerColor.a));
            }
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }
            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex LitGBufferPassVertex
            #pragma fragment CrawlerGBufferPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #define LitGBufferPassFragment BaseLitGBufferPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            #undef LitGBufferPassFragment

            GBufferFragOutput CrawlerGBufferPassFragment(Varyings input)
            {
                GBufferFragOutput output = BaseLitGBufferPassFragment(input);
                float mask = CrawlerSurfaceMask(input.positionWS, input.normalWS);
                output.gBuffer0.rgb = lerp(output.gBuffer0.rgb, _CrawlerColor.rgb,
                    saturate(mask * _CrawlerColor.a));
                return output;
            }
            ENDHLSL
        }

        // Geometry-related passes remain the standard URP Lit implementations.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

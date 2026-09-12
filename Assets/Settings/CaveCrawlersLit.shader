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
        _CrawlerPopulation ("Occupied Cells", Range(0, 1)) = 0.5
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

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        // Keep the standard URP Lit surface, shadows, depth and deferred support.
        UsePass "Universal Render Pipeline/Lit/ForwardLit"
        UsePass "Universal Render Pipeline/Lit/GBuffer"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"

        // The existing DitherAccentRendererFeature draws this sparse mask once per frame.
        Pass
        {
            Name "DitherAccent"
            Tags { "LightMode" = "DitherAccent" }
            ZWrite Off
            ZTest Equal
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CrawlerVertex
            #pragma fragment CrawlerFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CrawlerVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash21(float2 value)
            {
                float3 p = frac(float3(value.x, value.y, value.x) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float CapsuleDistance(float2 samplePosition, float2 segmentStart, float2 segmentFinish, float radius)
            {
                float2 segment = segmentFinish - segmentStart;
                float projection = saturate(dot(samplePosition - segmentStart, segment) / max(dot(segment, segment), 0.00001));
                return length(samplePosition - segmentStart - segment * projection) - radius;
            }

            float2 WallCoordinates(float3 positionWS, half3 normalWS)
            {
                // Select the dominant horizontal normal so the second coordinate is always world height.
                // This avoids the stretched UVs produced by the procedural wall mesh.
                float useXProjection = step(abs(normalWS.z), abs(normalWS.x));
                return lerp(positionWS.xy, positionWS.zy, useXProjection);
            }

            float CrawlerMask(float2 surfacePosition)
            {
                float density = max(_CrawlerDensity, 0.001);
                float2 gridPosition = surfacePosition * density;
                float2 cell = floor(gridPosition);
                float2 localPosition = (frac(gridPosition) - 0.5) / density;

                float seed = Hash21(cell + _CrawlerSeed);
                float occupied = step(seed, _CrawlerPopulation);
                float directionSeed = Hash21(cell + _CrawlerSeed + 19.17);
                float speedSeed = Hash21(cell + _CrawlerSeed + 47.53);
                float phaseSeed = Hash21(cell + _CrawlerSeed + 83.91);

                float angle = directionSeed * TWO_PI;
                float2 direction = float2(cos(angle), sin(angle));
                float2 perpendicular = float2(-direction.y, direction.x);
                float phase = frac(_Time.y * _CrawlerSpeed * lerp(0.7, 1.3, speedSeed) + phaseSeed);
                float cellSize = rcp(density);
                float travel = (phase * 2.0 - 1.0) * cellSize * 0.78;
                float pathOffset = sin(phase * TWO_PI + seed * 11.0) * _CrawlerWiggle;
                float2 centre = direction * travel + perpendicular * pathOffset;

                float2 delta = localPosition - centre;
                float2 crawler = float2(dot(delta, direction), dot(delta, perpendicular));
                float size = max(_CrawlerSize, 0.001);
                crawler.y += sin(crawler.x * 24.0 / size + phase * TWO_PI * 2.0) * _CrawlerWiggle * 0.12;

                float body = CapsuleDistance(crawler, float2(-size * 0.38, 0),
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

            half4 CrawlerFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                float wallFacing = 1.0 - abs(normalWS.y);
                float wallMask = smoothstep(_CrawlerWallThreshold, min(1.0, _CrawlerWallThreshold + 0.2), wallFacing);

                float distanceToCamera = distance(input.positionWS, _WorldSpaceCameraPos);
                float fadeStart = max(0.0, _CrawlerMaxDistance - _CrawlerFadeRange);
                float distanceMask = 1.0 - smoothstep(fadeStart, _CrawlerMaxDistance, distanceToCamera);
                float mask = CrawlerMask(WallCoordinates(input.positionWS, normalWS)) * wallMask * distanceMask;

                return half4(_CrawlerColor.rgb, saturate(mask * _CrawlerColor.a));
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

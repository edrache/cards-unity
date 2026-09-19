Shader "Cards Unity/Cave Grass"
{
    Properties
    {
        _BaseColor("Root color", Color) = (0.075, 0.12, 0.035, 1)
        _TipColor("Tip color", Color) = (0.32, 0.40, 0.12, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                float4 _GrassPlayer;
                float4 _GrassTrail;
                float _GrassDrawDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 root:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half3 color:TEXCOORD2; half fog:TEXCOORD3; half3 vertexLight:TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 root = TransformObjectToWorld(input.root.xyz);
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float t = input.uv.y;
                float2 segment = _GrassPlayer.xz - _GrassTrail.xz;
                float along = saturate(dot(root.xz - _GrassTrail.xz, segment) / max(dot(segment, segment), 0.0001));
                float3 contact = lerp(_GrassTrail.xyz, _GrassPlayer.xyz, along);
                float2 away = root.xz - contact.xz;
                float distanceToPlayer = length(away);
                float bend = 1 - smoothstep(0.12, max(_GrassPlayer.w, 0.13), distanceToPlayer);
                bend *= step(0.01, _GrassPlayer.w) * (1 - smoothstep(0.6, 2.2, abs(root.y - contact.y)));
                float2 direction = away / max(distanceToPlayer, 0.001);
                float height = length(TransformObjectToWorldDir(float3(0, input.root.w, 0), false));
                position.xz += direction * bend * height * 0.8 * t * t;
                position.y -= bend * height * 0.55 * t * t;
                // A tiny independent sway breaks up static silhouettes; roots stay anchored.
                float phase = dot(root.xz, float2(1.73, 2.37));
                position.xz += float2(sin(_Time.y * 1.3 + phase), cos(_Time.y + phase)) * 0.025 * t * t;
                float fade = 1 - smoothstep(_GrassDrawDistance * 0.78, _GrassDrawDistance, distance(root, _WorldSpaceCameraPos));
                position = root + (position - root) * fade;
                output.positionWS = position;
                output.positionCS = TransformWorldToHClip(position);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = lerp(_BaseColor.rgb, _TipColor.rgb, t) * (0.85 + 0.15 * sin(phase));
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.vertexLight = VertexLighting(position, output.normalWS);
                return output;
            }
        ENDHLSL
        Pass
        {
            Name "GrassForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            half3 GrassLight(Light light, half3 normal)
            {
                // Thin, two-sided leaves transmit some light from either side.
                return light.color * light.distanceAttenuation * light.shadowAttenuation * (0.25h + 0.75h * abs(dot(normal, light.direction)));
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 lighting = max(SampleSH(half3(0, 1, 0)), 0);
                lighting += GrassLight(GetMainLight(TransformWorldToShadowCoord(input.positionWS)), normal);
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                            lighting += GrassLight(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal);
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        lighting += GrassLight(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)), normal);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting += input.vertexLight;
                #endif
                return half4(MixFog(input.color * lighting, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFrag(Varyings input, FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(facing, 1, -1);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
}

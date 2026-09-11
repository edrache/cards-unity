Shader "CardsUnity/Dither Accent"
{
    Properties
    {
        [ToggleUI] _AccentEnabled ("Use Accent Color", Float) = 1
        _PickupOutlineColor ("Pickup Outline Color", Color) = (0.1, 0.9, 1, 1)
        _PickupOutlineWidth ("Pickup Outline Width (Pixels)", Range(0, 6)) = 2
        [MainColor] _BaseColor ("Accent Color", Color) = (1, 0.78, 0.02, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
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
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        // Reuse URP lighting, shadows and depth. The mask shares Lit's material buffer layout.
        UsePass "Universal Render Pipeline/Lit/ForwardLit"
        UsePass "Universal Render Pipeline/Lit/GBuffer"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
        Pass
        {
            Name "DitherAccent"
            Tags { "LightMode" = "DitherAccent" }
            ZWrite Off
            ZTest Equal
            Cull Back
            HLSLPROGRAM
            #pragma vertex AccentVertex
            #pragma fragment AccentFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings AccentVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            float _AccentEnabled;
            half4 AccentFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_BaseColor.rgb, saturate(_AccentEnabled));
            }
            ENDHLSL
        }
        Pass
        {
            Name "PickupOutline"
            Tags { "LightMode" = "PickupOutline" }
            ZWrite Off
            ZTest Equal
            Cull Back
            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            float4 _PickupOutlineColor;
            float _PickupOutlineWidth;
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_PickupOutlineColor.rgb, clamp(_PickupOutlineWidth, 0, 6) / 8.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

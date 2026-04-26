Shader "CardsUnity/UI/Rect Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StrokeMode ("Stroke Mode", Float) = 0
        _DashFitMode ("Dash Fit Mode", Float) = 0
        _DashLength ("Dash Length", Float) = 18
        _GapLength ("Gap Length", Float) = 10
        _DashOffset ("Dash Offset", Float) = 0
        _LineThickness ("Line Thickness", Float) = 6
        _CornerRadius ("Corner Radius", Float) = 0
        _RectSize ("Rect Size", Vector) = (100,100,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _StrokeMode;
            float _DashFitMode;
            float _DashLength;
            float _GapLength;
            float _DashOffset;
            float _LineThickness;
            float _CornerRadius;
            float4 _RectSize;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.localPosition = v.localPosition;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SignedDistanceRoundedRect(float2 samplePos, float2 halfSize, float radius)
            {
                float2 q = abs(samplePos) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float CalculatePerimeter(float width, float height, float radius)
            {
                float horizontal = max(width - (2.0 * radius), 0.0);
                float vertical = max(height - (2.0 * radius), 0.0);
                return (2.0 * horizontal) + (2.0 * vertical) + (2.0 * UNITY_PI * radius);
            }

            float CalculateEdgeDistance(float2 samplePos, float2 halfSize, float radius)
            {
                float innerHalfX = max(halfSize.x - radius, 0.0);
                float innerHalfY = max(halfSize.y - radius, 0.0);
                float horizontal = max((halfSize.x * 2.0) - (2.0 * radius), 0.0);
                float vertical = max((halfSize.y * 2.0) - (2.0 * radius), 0.0);
                float cornerArc = 0.5 * UNITY_PI * radius;
                float2 absPoint = abs(samplePos);
                float2 cornerCenter = float2(innerHalfX, innerHalfY);
                float2 cornerDelta = max(absPoint - cornerCenter, 0.0);

                if (samplePos.y >= innerHalfY && absPoint.x <= innerHalfX)
                {
                    return radius + (samplePos.x + innerHalfX);
                }

                if (samplePos.x >= innerHalfX && samplePos.y >= innerHalfY)
                {
                    float angle = atan2(cornerDelta.y, cornerDelta.x);
                    return radius + horizontal + ((0.5 * UNITY_PI - angle) * radius);
                }

                if (samplePos.x >= innerHalfX && absPoint.y <= innerHalfY)
                {
                    return radius + horizontal + cornerArc + (innerHalfY - samplePos.y);
                }

                if (samplePos.x >= innerHalfX && samplePos.y <= -innerHalfY)
                {
                    float angle = atan2(-cornerDelta.y, cornerDelta.x);
                    return radius + horizontal + cornerArc + vertical + ((0.5 * UNITY_PI - angle) * radius);
                }

                if (samplePos.y <= -innerHalfY && absPoint.x <= innerHalfX)
                {
                    return radius + horizontal + cornerArc + vertical + cornerArc + (innerHalfX - samplePos.x);
                }

                if (samplePos.x <= -innerHalfX && samplePos.y <= -innerHalfY)
                {
                    float angle = atan2(-cornerDelta.y, -cornerDelta.x);
                    return radius + horizontal + cornerArc + vertical + cornerArc + horizontal + ((0.5 * UNITY_PI - angle) * radius);
                }

                if (samplePos.x <= -innerHalfX && absPoint.y <= innerHalfY)
                {
                    return radius + horizontal + cornerArc + vertical + cornerArc + horizontal + cornerArc + (samplePos.y + innerHalfY);
                }

                float topLeftArcStart = radius + horizontal + cornerArc + vertical + cornerArc + horizontal + cornerArc + vertical;
                float angleTopLeft = atan2(cornerDelta.y, -cornerDelta.x);
                return topLeftArcStart + ((0.5 * UNITY_PI - angleTopLeft) * radius);
            }

            float EvaluateDashMask(float along, float edgeLength)
            {
                if (_StrokeMode < 0.5)
                    return 1.0;

                float dashLength = max(_DashLength, 0.001);
                float gapLength = max(_GapLength, 0.0);
                float patternLength = max(dashLength + gapLength, 0.001);
                float dashRatio = saturate(dashLength / patternLength);

                if (_DashFitMode > 0.5)
                {
                    float repeatCount = max(1.0, round(edgeLength / patternLength));
                    patternLength = edgeLength / repeatCount;
                }

                float patternPosition = frac((along + _DashOffset) / patternLength);
                float feather = max(fwidth(patternPosition) * 1.5, 0.001);
                return 1.0 - smoothstep(dashRatio - feather, dashRatio + feather, patternPosition);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = IN.color;
                float2 halfSize = _RectSize.xy * 0.5;
                float thickness = max(_LineThickness, 0.0);
                float radius = clamp(_CornerRadius, 0.0, min(halfSize.x, halfSize.y));
                float2 localPoint = IN.localPosition;
                float signedDistance = SignedDistanceRoundedRect(localPoint, halfSize, radius);
                float aa = max(fwidth(signedDistance), 0.001);
                float outerMask = 1.0 - smoothstep(0.0, aa, signedDistance);
                float innerMask = smoothstep(-thickness - aa, -thickness + aa, signedDistance);
                color.a *= outerMask * innerMask;

                float perimeter = max(CalculatePerimeter(_RectSize.x, _RectSize.y, radius), 0.001);
                float along = CalculateEdgeDistance(localPoint, halfSize, radius);
                color.a *= EvaluateDashMask(along, perimeter);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}

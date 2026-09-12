Shader "CardsUnity/UI/Render Texture Composite"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Render Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        // uGUI renders premultiplied color into the transparent target.
        // Multiplying by alpha again would darken translucent panels and glyph edges.
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                // Apply future UI-only effects here, preserving premultiplied alpha.
                fixed4 color = tex2D(_MainTex, input.uv);
                color.rgb *= input.color.rgb * input.color.a;
                color.a *= input.color.a;
                return color;
            }
            ENDCG
        }
    }
}

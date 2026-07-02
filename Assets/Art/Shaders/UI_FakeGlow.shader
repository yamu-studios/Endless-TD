Shader "UI/URP/GlowUI_Updated"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Tint Color", Color) = (1,1,1,1)

        [HDR] _GlowColor ("Glow Color", Color) = (0.2, 0.8, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2
        _BaseStrength ("Base Strength", Range(0, 2)) = 1

        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1

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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            Name "GlowUI"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;

            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _BaseStrength;
            float _AlphaMultiplier;
            float4 _ClipRect;

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                float alpha = texColor.a * IN.color.a * _AlphaMultiplier;

                float3 spriteColor = texColor.rgb * IN.color.rgb;

                // Base visible UI color
                float3 baseColor = spriteColor * _BaseStrength;

                // Extra HDR color that triggers Bloom
                float3 glowColor = spriteColor * _GlowColor.rgb * _GlowIntensity;

                float3 finalColor = baseColor + glowColor;

                fixed4 finalOutput = fixed4(finalColor, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                    finalOutput.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(finalOutput.a - 0.001);
                #endif

                return finalOutput;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
Shader "Game/Laser Turret Beam URP"
{
    Properties
    {
        [HDR]_CoreColor("Core Color", Color) = (2, 0.15, 0.15, 1)
        [HDR]_GlowColor("Glow Color", Color) = (1, 0.4, 0.4, 1)

        _MainTex("Noise / Beam Texture", 2D) = "white" {}
        _BeamWidth("Beam Width", Range(0.01, 1)) = 0.22
        _CoreWidth("Core Width", Range(0.001, 1)) = 0.06
        _GlowPower("Glow Power", Range(0.2, 8)) = 2.5
        _Brightness("Brightness", Range(0, 20)) = 4

        _ScrollSpeed("Scroll Speed", Range(-10, 10)) = 3
        _PulseSpeed("Pulse Speed", Range(0, 20)) = 6
        _PulseAmount("Pulse Amount", Range(0, 2)) = 0.25

        _NoiseAmount("Noise Amount", Range(0, 2)) = 0.2
        _Alpha("Alpha", Range(0, 5)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float4 _MainTex_ST;

                float _BeamWidth;
                float _CoreWidth;
                float _GlowPower;
                float _Brightness;

                float _ScrollSpeed;
                float _PulseSpeed;
                float _PulseAmount;

                float _NoiseAmount;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float remap01(float a, float b, float v)
            {
                return saturate((v - a) / max(0.0001, (b - a)));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Beam runs along U, width is V centered at 0.5
                float beamCenterDist = abs(uv.y - 0.5) * 2.0;

                // Scrolling texture for energy detail
                float2 scrollUV = float2(uv.x + _Time.y * _ScrollSpeed, uv.y);
                float noise = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV).r;

                // Subtle pulse
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + uv.x * 10.0) * _PulseAmount;

                // Slight width wobble from noise
                float widthNoise = (noise - 0.5) * _NoiseAmount;

                float beamWidth = max(0.001, _BeamWidth + widthNoise * 0.15);
                float coreWidth = max(0.001, min(_CoreWidth, beamWidth));

                // Convert desired widths into edge masks
                float outerMask = 1.0 - remap01(0.0, beamWidth, beamCenterDist);
                outerMask = pow(saturate(outerMask), _GlowPower);

                float coreMask = 1.0 - remap01(0.0, coreWidth, beamCenterDist);
                coreMask = pow(saturate(coreMask), 1.5);

                // Longitudinal shimmer
                float shimmer = 0.8 + 0.2 * sin(uv.x * 30.0 - _Time.y * (_ScrollSpeed * 3.0));
                shimmer *= lerp(0.85, 1.15, noise);

                float3 glow = _GlowColor.rgb * outerMask;
                float3 core = _CoreColor.rgb * coreMask * 1.5;

                float3 finalRgb = (glow + core) * _Brightness * pulse * shimmer * IN.color.rgb;
                float finalA = saturate(max(outerMask, coreMask) * _Alpha) * IN.color.a;

                return half4(finalRgb * finalA, finalA);
            }
            ENDHLSL
        }
    }
}
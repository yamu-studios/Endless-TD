Shader "Game/Laser Turret Muzzle URP"
{
    Properties
    {
        [HDR]_CoreColor("Core Color", Color) = (4, 0.3, 0.3, 1)
        [HDR]_GlowColor("Glow Color", Color) = (2, 0.8, 0.4, 1)

        _MainTex("Shape Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "gray" {}

        _Brightness("Brightness", Range(0, 20)) = 5
        _Alpha("Alpha", Range(0, 5)) = 1

        _CoreSize("Core Size", Range(0.01, 1)) = 0.18
        _GlowSize("Glow Size", Range(0.01, 1)) = 0.45
        _GlowPower("Glow Power", Range(0.2, 8)) = 2.5

        _ScrollSpeed("Scroll Speed", Range(-10, 10)) = 2
        _NoiseAmount("Noise Amount", Range(0, 2)) = 0.35

        _PulseSpeed("Pulse Speed", Range(0, 20)) = 8
        _PulseAmount("Pulse Amount", Range(0, 2)) = 0.18

        _ForwardBias("Forward Bias", Range(-1, 1)) = 0.2
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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;

                float _Brightness;
                float _Alpha;

                float _CoreSize;
                float _GlowSize;
                float _GlowPower;

                float _ScrollSpeed;
                float _NoiseAmount;

                float _PulseSpeed;
                float _PulseAmount;

                float _ForwardBias;
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
                return saturate((v - a) / max(0.0001, b - a));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Shifted center slightly forward so it feels emitted from the barrel
                float2 center = float2(0.5 + _ForwardBias * 0.15, 0.5);
                float2 delta = uv - center;

                float dist = length(delta);

                float2 noiseUV = TRANSFORM_TEX(
                    float2(uv.x + _Time.y * _ScrollSpeed, uv.y - _Time.y * _ScrollSpeed * 0.35),
                    _NoiseTex
                );

                float shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float noisyGlowSize = max(0.001, _GlowSize + (noise - 0.5) * _NoiseAmount * 0.15);
                float noisyCoreSize = max(0.001, _CoreSize + (noise - 0.5) * _NoiseAmount * 0.08);

                float glowMask = 1.0 - remap01(0.0, noisyGlowSize, dist);
                glowMask = pow(saturate(glowMask), _GlowPower);

                float coreMask = 1.0 - remap01(0.0, noisyCoreSize, dist);
                coreMask = pow(saturate(coreMask), 1.6);

                // Forward flare shaping
                float forward = saturate(uv.x);
                float forwardBoost = lerp(0.75, 1.25, forward);

                float energy = lerp(1.0, noise, _NoiseAmount) * shape;

                float3 glow = _GlowColor.rgb * glowMask;
                float3 core = _CoreColor.rgb * coreMask * 1.6;

                float3 finalRgb = (glow + core) * energy * pulse * forwardBoost * _Brightness * IN.color.rgb;
                float finalA = saturate(max(glowMask, coreMask) * _Alpha * shape) * IN.color.a;

                return half4(finalRgb * finalA, finalA);
            }
            ENDHLSL
        }
    }
}
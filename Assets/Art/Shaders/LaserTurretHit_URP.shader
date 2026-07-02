Shader "Game/Laser Turret Hit URP"
{
    Properties
    {
        [HDR]_CoreColor("Core Color", Color) = (5, 0.5, 0.3, 1)
        [HDR]_RingColor("Ring Color", Color) = (3, 1.2, 0.4, 1)
        [HDR]_SmokeGlowColor("Smoke Glow Color", Color) = (1.2, 0.4, 0.25, 1)

        _MainTex("Shape Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "gray" {}

        _Brightness("Brightness", Range(0, 20)) = 5
        _Alpha("Alpha", Range(0, 5)) = 1

        _CoreRadius("Core Radius", Range(0.01, 1)) = 0.12
        _RingRadius("Ring Radius", Range(0.01, 1)) = 0.32
        _RingWidth("Ring Width", Range(0.001, 0.5)) = 0.08

        _NoiseAmount("Noise Amount", Range(0, 2)) = 0.35
        _ScrollSpeed("Scroll Speed", Range(-10, 10)) = 1.5

        _PulseSpeed("Pulse Speed", Range(0, 20)) = 10
        _PulseAmount("Pulse Amount", Range(0, 2)) = 0.2

        _OuterFade("Outer Fade", Range(0.01, 1)) = 0.55
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
                float4 _RingColor;
                float4 _SmokeGlowColor;
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;

                float _Brightness;
                float _Alpha;

                float _CoreRadius;
                float _RingRadius;
                float _RingWidth;

                float _NoiseAmount;
                float _ScrollSpeed;

                float _PulseSpeed;
                float _PulseAmount;

                float _OuterFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float ringMask(float dist, float radius, float width)
            {
                float inner = smoothstep(radius - width, radius, dist);
                float outer = 1.0 - smoothstep(radius, radius + width, dist);
                return saturate(inner * outer);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 p = uv - 0.5;
                float dist = length(p);

                float2 noiseUV1 = TRANSFORM_TEX(float2(uv.x + _Time.y * _ScrollSpeed, uv.y), _NoiseTex);
                float2 noiseUV2 = TRANSFORM_TEX(float2(uv.x - _Time.y * _ScrollSpeed * 0.6, uv.y + _Time.y * _ScrollSpeed * 0.4), _NoiseTex);

                float shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
                float noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV1).r;
                float noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV2).r;
                float noise = lerp(noiseA, noiseB, 0.5);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float core = 1.0 - smoothstep(0.0, _CoreRadius, dist);
                core = pow(saturate(core), 1.8);

                float ringR = _RingRadius + (noise - 0.5) * _NoiseAmount * 0.08;
                float ring = ringMask(dist, ringR, _RingWidth);

                float outer = 1.0 - smoothstep(0.0, _OuterFade, dist);
                outer = saturate(outer);

                float turbulentOuter = outer * lerp(1.0, noise, _NoiseAmount);

                float3 coreCol = _CoreColor.rgb * core * 1.6;
                float3 ringCol = _RingColor.rgb * ring * 1.4;
                float3 smokeGlow = _SmokeGlowColor.rgb * turbulentOuter * 0.7;

                float3 finalRgb = (coreCol + ringCol + smokeGlow) * shape * pulse * _Brightness * IN.color.rgb;
                float finalA = saturate(max(core, max(ring, turbulentOuter * 0.7)) * shape * _Alpha) * IN.color.a;

                return half4(finalRgb * finalA, finalA);
            }
            ENDHLSL
        }
    }
}
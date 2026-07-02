Shader "Game/Basic Turret Projectile URP"
{
    Properties
    {
        [HDR]_CoreColor("Core Color", Color) = (0.4, 1.5, 4.0, 1)
        [HDR]_RimColor("Rim Color", Color) = (0.2, 0.8, 2.0, 1)

        _MainTex("Projectile Texture", 2D) = "white" {}
        _NoiseTex("Noise Texture", 2D) = "gray" {}

        _Brightness("Brightness", Range(0, 20)) = 3.5
        _Alpha("Alpha", Range(0, 5)) = 1.0

        _RimPower("Rim Power", Range(0.2, 8)) = 2.5
        _CoreSharpness("Core Sharpness", Range(0.5, 8)) = 3.0

        _ScrollSpeed("Scroll Speed", Range(-10, 10)) = 2.0
        _NoiseAmount("Noise Amount", Range(0, 2)) = 0.25

        _LengthFade("Length Fade", Range(0.01, 1)) = 0.18
        _PulseSpeed("Pulse Speed", Range(0, 20)) = 7.0
        _PulseAmount("Pulse Amount", Range(0, 2)) = 0.15
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _RimColor;
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;

                float _Brightness;
                float _Alpha;

                float _RimPower;
                float _CoreSharpness;

                float _ScrollSpeed;
                float _NoiseAmount;

                float _LengthFade;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = normalize(normInputs.normalWS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.color = IN.color;

                return OUT;
            }

            float edgeMask(float x, float fade)
            {
                float left = smoothstep(0.0, fade, x);
                float right = 1.0 - smoothstep(1.0 - fade, 1.0, x);
                return saturate(left * right);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel-style rim
                float ndv = saturate(dot(N, V));
                float rim = pow(1.0 - ndv, _RimPower);

                // Projectile body mask from UV center
                float radial = 1.0 - saturate(abs(uv.y - 0.5) * 2.0);
                float coreMask = pow(radial, _CoreSharpness);

                // Front/back fade so it looks like a bolt rather than a flat glowing pill
                float lengthMask = edgeMask(uv.x, _LengthFade);

                // Animated texture detail
                float2 mainUV = uv;
                float2 noiseUV = TRANSFORM_TEX(float2(uv.x + _Time.y * _ScrollSpeed, uv.y), _NoiseTex);

                float mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV).r;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + uv.x * 8.0) * _PulseAmount;

                float energy = lerp(1.0, noise, _NoiseAmount) * mainTex;

                float3 core = _CoreColor.rgb * coreMask * energy;
                float3 glow = _RimColor.rgb * rim * (0.5 + 0.5 * energy);

                float mask = saturate(max(coreMask, rim * 0.7) * lengthMask);

                float3 finalRgb = (core + glow) * _Brightness * pulse * IN.color.rgb;
                float finalA = mask * _Alpha * IN.color.a;

                return half4(finalRgb * finalA, finalA);
            }
            ENDHLSL
        }
    }
}
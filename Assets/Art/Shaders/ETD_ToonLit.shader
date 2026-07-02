Shader "ETD/ToonLit"
{
    Properties
    {
        _BaseColor          ("Base Color", Color) = (1,1,1,1)
        _BaseMap            ("Base Map", 2D) = "white" {}

        _ShadowColor        ("Shadow Color", Color) = (0.3,0.25,0.35,1)
        _ShadowThreshold    ("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowSmoothness   ("Shadow Smoothness", Range(0,0.5)) = 0.05

        _RimColor           ("Rim Color", Color) = (1,1,1,1)
        _RimPower           ("Rim Power", Range(0.5,8)) = 3
        _RimStrength        ("Rim Strength", Range(0,1)) = 0.3

        _OutlineColor       ("Outline Color", Color) = (0.1,0.1,0.1,1)
        _OutlineWidth       ("Outline Width", Range(0,0.05)) = 0.01

        [Toggle] _UseEmission ("Use Emission", Float) = 0
        _EmissionColor      ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength   ("Emission Strength", Range(0,3)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        // =====================================================
        // MAIN TOON PASS
        // =====================================================

        Pass
        {
            Name "ToonLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // MINIMAL VARIANTS
            #pragma shader_feature_local _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseColor;

                float4 _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSmoothness;

                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;

                float4 _OutlineColor;
                float  _OutlineWidth;

                float  _UseEmission;
                float4 _EmissionColor;
                float  _EmissionStrength;

            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 positionWS : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = normalize(normInputs.normalWS);
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv         = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                half3 baseColor = tex.rgb * _BaseColor.rgb;

                float3 normal  = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);

                // =========================================
                // MAIN LIGHT
                // =========================================

                float4 shadowCoord = 0;

                #ifdef _MAIN_LIGHT_SHADOWS
                    shadowCoord =
                        TransformWorldToShadowCoord(input.positionWS);
                #endif

                Light mainLight = GetMainLight(shadowCoord);

                float NdotL =
                    dot(normal, normalize(mainLight.direction));

                float ramp =
                    smoothstep(
                        _ShadowThreshold - _ShadowSmoothness,
                        _ShadowThreshold + _ShadowSmoothness,
                        NdotL * mainLight.shadowAttenuation
                    );

                half3 finalColor =
                    lerp(
                        baseColor * _ShadowColor.rgb,
                        baseColor * mainLight.color.rgb,
                        ramp
                    );

                // =========================================
                // RIM LIGHT
                // =========================================

                float rim =
                    pow(
                        1.0 - saturate(dot(viewDir, normal)),
                        _RimPower
                    ) * _RimStrength;

                finalColor += _RimColor.rgb * rim;

                // =========================================
                // EMISSION
                // =========================================

                if (_UseEmission > 0.5)
                {
                    finalColor +=
                        _EmissionColor.rgb * _EmissionStrength;
                }

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }

        // =====================================================
        // OUTLINE PASS
        // =====================================================

        Pass
        {
            Name "Outline"

            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Front

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)

                float4 _OutlineColor;
                float  _OutlineWidth;

            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos =
                    input.positionOS.xyz +
                    input.normalOS * _OutlineWidth;

                output.positionCS =
                    TransformObjectToHClip(pos);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }

        // =====================================================
        // SHADOW CASTER
        // =====================================================

        Pass
        {
            Name "ShadowCaster"

            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);

                float3 posWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                float3 normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                output.positionCS =
                    TransformWorldToHClip(
                        ApplyShadowBias(
                            posWS,
                            normalWS,
                            _LightDirection
                        )
                    );

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}
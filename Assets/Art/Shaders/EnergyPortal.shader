Shader "Custom/URP/EnergyPortal"
{
    Properties
    {
        [HDR] _ColorA ("Primary Color", Color) = (0.2, 1.0, 0.4, 1)
[HDR] _ColorB ("Secondary Color", Color) = (0.8, 1.0, 0.9, 1)
[HDR] _EdgeColor ("Edge Glow Color", Color) = (1, 1, 1, 1)

        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _FlowTex ("Flow Texture", 2D) = "gray" {}

        _Tiling ("Tiling", Float) = 2.0
        _FlowSpeed1 ("Flow Speed 1", Vector) = (0.12, 0.25, 0, 0)
        _FlowSpeed2 ("Flow Speed 2", Vector) = (-0.18, 0.10, 0, 0)

        _Distortion ("UV Distortion", Range(0, 1)) = 0.08
        _VeinPower ("Vein Contrast", Range(0.5, 8)) = 3.5
        _Brightness ("Brightness", Range(0, 10)) = 3.0
        _Opacity ("Opacity", Range(0, 1)) = 0.75

        _EdgePower ("Edge Power", Range(0.1, 8)) = 2.5
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 1.5

        _DepthFadeDistance ("Depth Fade Distance", Range(0.001, 2)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Needed for soft depth fade
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_FlowTex);
            SAMPLER(sampler_FlowTex);

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _EdgeColor;

                float4 _FlowSpeed1;
                float4 _FlowSpeed2;

                float _Tiling;
                float _Distortion;
                float _VeinPower;
                float _Brightness;
                float _Opacity;

                float _EdgePower;
                float _EdgeIntensity;

                float _DepthFadeDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 screenPos    : TEXCOORD3;
                float  viewDepth    : TEXCOORD4;
            };

            float remap(float v, float inMin, float inMax, float outMin, float outMax)
            {
                return outMin + (v - inMin) * (outMax - outMin) / (inMax - inMin);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.viewDepth = -TransformWorldToView(posInputs.positionWS).z;

                return OUT;
            }

           half4 frag(Varyings IN) : SV_Target
{
    float2 uv = IN.uv * _Tiling;
    float t = _Time.y;

    float2 uv1 = uv + _FlowSpeed1.xy * t;
    float2 uv2 = uv + _FlowSpeed2.xy * t;

    float2 distortSample = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uv1).rg * 2.0 - 1.0;
    float2 distortedUV = uv + distortSample * _Distortion;

    float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, distortedUV + _FlowSpeed1.xy * t).r;
    float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, distortedUV + _FlowSpeed2.xy * t).r;
    float n3 = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, uv2).r;

    // More stable moving pattern
    float veins = 1.0 - abs(n1 - n2);
    veins = saturate(veins);

    // Reduce full-screen intensity swings
    float detailMask = lerp(0.75, 1.25, n3);
    float pattern = saturate(pow(veins, _VeinPower) * detailMask);

    float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
    float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), viewDirWS)), _EdgePower);

    float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
    float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
    float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);

    float depthDiff = sceneEyeDepth - IN.viewDepth;
    float depthFade = saturate(depthDiff / _DepthFadeDistance);

    float3 baseCol = lerp(_ColorA.rgb, _ColorB.rgb, pattern);

    // Stable base emission + moving detail on top
    float stableBase = 1.0;
    float movingGlow = pattern * 0.8;
    float edgeGlow = fresnel * _EdgeIntensity;

    float3 finalCol = baseCol * (stableBase + movingGlow) * _Brightness;
    finalCol += _EdgeColor.rgb * edgeGlow;

    float alpha = saturate((_Opacity + pattern * 0.15 + fresnel * 0.2) * depthFade);

    return half4(finalCol, alpha);
}
            ENDHLSL
        }
    }
}
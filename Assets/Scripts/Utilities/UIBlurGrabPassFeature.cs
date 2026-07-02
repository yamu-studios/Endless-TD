using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class UIBlurGrabPassFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public bool enabled = true;

        [Range(0f, 10f)]
        public float blurRadius = 4f;

        [Range(0.25f, 1f)]
        public float resolutionScale = 0.5f;

        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();
    public Shader blurShader;

    private Material blurMaterial;
    private FullscreenBlurPass blurPass;

    public override void Create()
    {
        if (blurShader == null)
            blurShader = Shader.Find("Hidden/URP/FullscreenBlur");

        if (blurShader == null)
        {
            Debug.LogError("Fullscreen blur shader not found.");
            return;
        }

        blurMaterial = CoreUtils.CreateEngineMaterial(blurShader);

        blurPass = new FullscreenBlurPass(blurMaterial, settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!settings.enabled)
            return;

        if (blurPass == null || blurMaterial == null)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        blurPass.renderPassEvent = settings.passEvent;
        renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(blurMaterial);
    }

    private class FullscreenBlurPass : ScriptableRenderPass
    {
        private static readonly int BlurRadiusID = Shader.PropertyToID("_BlurRadius");

        private readonly Material material;
        private readonly Settings settings;

        public FullscreenBlurPass(Material material, Settings settings)
        {
            this.material = material;
            this.settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle cameraColor = resourceData.activeColorTexture;

            if (!cameraColor.IsValid())
                return;

            material.SetFloat(BlurRadiusID, settings.blurRadius);

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            descriptor.width = Mathf.Max(1, Mathf.RoundToInt(descriptor.width * settings.resolutionScale));
            descriptor.height = Mathf.Max(1, Mathf.RoundToInt(descriptor.height * settings.resolutionScale));

            TextureHandle tempA = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "_FullscreenBlurTempA",
                false
            );

            TextureHandle tempB = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "_FullscreenBlurTempB",
                false
            );

            var vertical = new RenderGraphUtils.BlitMaterialParameters(cameraColor, tempA, material, 0);
            renderGraph.AddBlitPass(vertical, "Fullscreen Blur Vertical");

            var horizontal = new RenderGraphUtils.BlitMaterialParameters(tempA, tempB, material, 1);
            renderGraph.AddBlitPass(horizontal, "Fullscreen Blur Horizontal");

            var copyBack = new RenderGraphUtils.BlitMaterialParameters(tempB, cameraColor, material, 2);
            renderGraph.AddBlitPass(copyBack, "Fullscreen Blur Copy Back");
        }
    }
}
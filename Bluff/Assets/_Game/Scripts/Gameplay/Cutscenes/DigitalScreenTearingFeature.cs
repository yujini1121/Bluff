using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public sealed class DigitalScreenTearingFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    private Material material;
    private TearingPass pass;

    public override void Create()
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new TearingPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (material == null || !material.shader.isSupported ||
            data.cameraData.cameraType != CameraType.Game ||
            data.cameraData.renderType != CameraRenderType.Base) return;
        var effect = DigitalScreenTearing.ForCamera(data.cameraData.camera);
        if (effect == null) { pass?.Dispose(); return; }
        pass.effect = effect;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose(); pass = null;
        CoreUtils.Destroy(material); material = null;
    }

    private sealed class TearingPass : ScriptableRenderPass
    {
        public DigitalScreenTearing effect;
        private readonly Material material;
        private RTHandle copy;
        private static readonly int Tear = Shader.PropertyToID("_Tear");
        private static readonly int Spread = Shader.PropertyToID("_Spread");
        private static readonly int Pattern = Shader.PropertyToID("_Pattern");

        public TearingPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            profilingSampler = new ProfilingSampler("Defeat Digital Screen Tearing");
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            ResetTarget();
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = descriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateIfNeeded(ref copy, descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_DefeatTearingCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (copy == null || effect == null ||
                DigitalScreenTearing.ForCamera(data.cameraData.camera) != effect) return;
            material.SetVector(Tear, new Vector4(Mathf.Clamp01(effect.intensity),
                Mathf.Clamp(effect.horizontalOffset, -0.1f, 0.1f) * Mathf.Clamp(effect.glitchDisplacement, 0, 4),
                Mathf.Clamp(effect.bandHeight, 0.005f, 0.25f), Mathf.Clamp(effect.rgbSeparation, 0, 0.01f)));
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(effect.glitchDensity) *
                Mathf.Clamp(effect.maxGlitches, 1, 30)), 1, 30);
            material.SetVector(Spread, new Vector4(count, Mathf.Clamp01(effect.glitchCoverage),
                Mathf.Clamp01(effect.glitchSizeVariation), 0));
            material.SetVector(Pattern, new Vector4(Mathf.Floor(Mathf.Max(0, effect.glitchPatternPhase) *
                Mathf.Clamp(effect.glitchPatternSpeed, 0.1f, 3)), 0, 0, 0));
            var cmd = CommandBufferPool.Get();
            try
            {
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var source = data.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, source, copy);
                    Blitter.BlitCameraTexture(cmd, copy, source, material, 0);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        public override void OnCameraCleanup(CommandBuffer cmd) { effect = null; }
        public void Dispose() { copy?.Release(); copy = null; effect = null; }
    }
}

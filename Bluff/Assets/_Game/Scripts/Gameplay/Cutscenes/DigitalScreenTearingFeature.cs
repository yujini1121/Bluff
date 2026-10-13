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
        if (effect == null) return;
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
        private static readonly int Split = Shader.PropertyToID("_Split");

        public TearingPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            profilingSampler = new ProfilingSampler("Defeat Screen Split");
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
                TextureWrapMode.Clamp, name: "_DefeatSplitCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (copy == null || effect == null ||
                DigitalScreenTearing.ForCamera(data.cameraData.camera) != effect) return;
            material.SetVector(Split, new Vector4(Mathf.Clamp01(effect.intensity),
                Mathf.Clamp01(effect.crackProgress), Mathf.Clamp(effect.crackWidth, 0, 12),
                Mathf.Clamp01(effect.separationProgress) * Mathf.Clamp(effect.displacement, 0, 0.06f)));
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
        public override void OnCameraCleanup(CommandBuffer cmd) => Dispose();
        public void Dispose() { copy?.Release(); copy = null; effect = null; }
    }
}

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CardsUnity.Rendering
{
    /// <summary>Runs the existing dither material with a depth-tested, per-material accent palette.</summary>
    public sealed class DitherAccentRendererFeature : ScriptableRendererFeature
    {
        public Material ditherMaterial;
        private AccentPass accentPass;
        private DitherPass ditherPass;

        public override void Create()
        {
            accentPass = new AccentPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            ditherPass = new DitherPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (ditherMaterial == null || camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection ||
                (camera.targetTexture != null && camera.targetTexture.format == RenderTextureFormat.Depth))
                return;

            ditherPass.material = ditherMaterial;
            renderer.EnqueuePass(accentPass);
            renderer.EnqueuePass(ditherPass);
        }

        // Frame data avoids global textures leaking between cameras or renderers.
        private sealed class AccentData : ContextItem
        {
            public TextureHandle texture, pickup;
            public override void Reset() { texture = pickup = TextureHandle.nullHandle; }
        }

        private sealed class AccentPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId AccentTag = new ShaderTagId("DitherAccent");
            private sealed class PassData { public RendererListHandle renderers; }
            private struct PickupDraw
            {
                public Renderer renderer;
                public Material material;
                public int submesh, pass;
            }
            private sealed class PickupPassData { public PickupDraw[] draws; }
            private Transform cachedTarget;
            private PickupDraw[] cachedDraws = System.Array.Empty<PickupDraw>();

            private PickupDraw[] PickupDraws(Camera camera)
            {
                var target = CardsUnity.Controllers.TorchInteraction.GetPickupTarget(camera);
                if (target == cachedTarget) return cachedDraws;
                cachedTarget = target;
                var draws = new System.Collections.Generic.List<PickupDraw>();
                if (target != null)
                    foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>())
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var material = materials[i];
                            int pass = material != null ? material.FindPass("PickupOutline") : -1;
                            if (pass >= 0) draws.Add(new PickupDraw {
                                renderer = renderer, material = material, submesh = i, pass = pass });
                        }
                    }
                cachedDraws = draws.ToArray();
                return cachedDraws;
            }


            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var rendering = frame.Get<UniversalRenderingData>();
                var lights = frame.Get<UniversalLightData>();
                // Match depth dimensions and sample count, including render scale and MSAA.
                var descriptor = graph.GetTextureDesc(resources.activeDepthTexture);
                descriptor.name = "Dither Accent Mask";
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                descriptor.bindTextureMS = false;
                descriptor.clearBuffer = true;
                descriptor.clearColor = Color.clear;
                descriptor.filterMode = FilterMode.Point;
                var texture = graph.CreateTexture(descriptor);
                frame.GetOrCreate<AccentData>().texture = texture;
                descriptor.name = "Pickup Outline Mask";
                var pickup = graph.CreateTexture(descriptor);
                frame.Get<AccentData>().pickup = pickup;
                using (var builder = graph.AddRasterRenderPass<PickupPassData>("Pickup Outline Mask", out var data))
                {
                    data.draws = PickupDraws(camera.camera);
                    builder.SetRenderAttachment(pickup, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    int cullingMask = camera.camera.cullingMask;
                    builder.SetRenderFunc((PickupPassData pass, RasterGraphContext context) =>
                    {
                        foreach (var draw in pass.draws)
                            if (draw.renderer != null && draw.renderer.enabled && draw.renderer.gameObject.activeInHierarchy
                                && (cullingMask & (1 << draw.renderer.gameObject.layer)) != 0)
                                context.cmd.DrawRenderer(draw.renderer, draw.material, draw.submesh, draw.pass);
                    });
                }


                var drawing = RenderingUtils.CreateDrawingSettings(AccentTag, rendering, camera, lights,
                    camera.defaultOpaqueSortFlags);
                var filtering = new FilteringSettings(RenderQueueRange.opaque, camera.camera.cullingMask);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                using (var builder = graph.AddRasterRenderPass<PassData>("Dither Accent Mask", out var data))
                {
                    data.renderers = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(texture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(pass.renderers));
                }
            }
        }

        private sealed class DitherPass : ScriptableRenderPass
        {
            public Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int PickupTexture = Shader.PropertyToID("_PickupOutlineTexture");
            private static readonly int AccentTexture = Shader.PropertyToID("_DitherAccentTexture");
            private static readonly int CameraUIRect = Shader.PropertyToID("_DitherCameraUIRect");
            private static readonly int AccentEnabled = Shader.PropertyToID("_DitherAccentEnabled");

            public DitherPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            private sealed class PassData
            {
                public TextureHandle source, accent, pickup;
                public Vector4 cameraUIRect;
                public Material material;
                public MaterialPropertyBlock properties;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
                descriptor.name = "Dither With Accents";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = DepthBits.None;
                var destination = graph.CreateTexture(descriptor);
                using (var builder = graph.AddRasterRenderPass<PassData>("One Bit Dither With Accents", out var data))
                {
                    data.cameraUIRect = CardsUnity.Controllers.CharacterInventory.GetDitherUIRect(
                        frame.Get<UniversalCameraData>().camera);
                    data.source = resources.activeColorTexture;
                    data.accent = frame.Get<AccentData>().texture;
                    data.pickup = frame.Get<AccentData>().pickup;
                    data.material = material;
                    data.properties = properties;
                    builder.UseTexture(data.source);
                    builder.UseTexture(data.accent);
                    builder.UseTexture(data.pickup);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData pass, RasterGraphContext context) =>
                    {
                        pass.properties.Clear();
                        pass.properties.SetTexture(BlitTexture, (RTHandle)pass.source);
                        pass.properties.SetTexture(AccentTexture, (RTHandle)pass.accent);
                        pass.properties.SetTexture(PickupTexture, (RTHandle)pass.pickup);
                        pass.properties.SetFloat(AccentEnabled, 1f);
                        pass.properties.SetVector(CameraUIRect, pass.cameraUIRect);
                        pass.properties.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 0,
                            MeshTopology.Triangles, 3, 1, pass.properties);
                    });
                }
                // Swap the output instead of copying the whole screen before the effect.
                resources.cameraColor = destination;
            }
        }
    }
}

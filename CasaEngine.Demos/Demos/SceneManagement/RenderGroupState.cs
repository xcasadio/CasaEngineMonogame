using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph
{
    public class RenderGroupState : IRenderGroupState
    {
        private IPipelineState PipelineState;
        private PrimitiveType PrimitiveType;
        private VertexDeclaration VertexLayout;

        public List<RenderGroupElement> Elements { get; } = new List<RenderGroupElement>();

        private Dictionary<GraphicsDevice, RenderInfo> RenderInfoCache;

        public static IRenderGroupState Create(IPipelineState pso, PrimitiveType pt, VertexDeclaration vertexLayout)
        {
            return new RenderGroupState(pso, pt, vertexLayout);
        }

        protected RenderGroupState(IPipelineState pso, PrimitiveType pt, VertexDeclaration vertexLayout)
        {
            PipelineState = pso;
            PrimitiveType = pt;
            VertexLayout = vertexLayout;
            RenderInfoCache = new Dictionary<GraphicsDevice, RenderInfo>();
        }

        public RenderInfo GetPipelineAndResources(GraphicsDevice graphicsDevice)
        {
            // TODO Cache this by device, factory
            if (RenderInfoCache.TryGetValue(graphicsDevice, out var ri))
            {
                return ri;
            }

            ri = new RenderInfo();
            /*
            var resourceLayoutElementDescriptionList = new List<ResourceLayoutElementDescription> { };
            var bindableResourceList = new List<BindableResource>();

            GraphicsPipelineDescription pd = new GraphicsPipelineDescription();
            pd.PrimitiveType = PrimitiveType;

            var nDrawables = (uint)Elements.Count;

            var alignment = graphicsDevice.UniformBufferMinOffsetAlignment;

            var modelViewMatrixObjSizeInBytes = 64u;
            if (alignment > 64u)
            {
                modelViewMatrixObjSizeInBytes = alignment;
            }

            ri.UniformStrides.Add(modelViewMatrixObjSizeInBytes);

            ri.ModelViewBuffer = graphicsDevice.CreateBuffer(new BufferDescription(modelViewMatrixObjSizeInBytes * nDrawables, BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            resourceLayoutElementDescriptionList.Add(
                new ResourceLayoutElementDescription("Model", ResourceKind.UniformBuffer, ShaderStages.Vertex, ResourceLayoutElementOptions.DynamicBinding));

            //bindableResourceList.Add(ri.ModelViewBuffer);
            bindableResourceList.Add(new DeviceBufferRange(ri.ModelViewBuffer, 0, modelViewMatrixObjSizeInBytes));

            // Process Attached Textures
            foreach (var tex2d in PipelineState.TextureList)
            {
                var deviceTexture = tex2d.ProcessedTexture.CreateDeviceTexture(graphicsDevice, resourceFactory, TextureUsage.Sampled);
                var textureView = resourceFactory.CreateTextureView(deviceTexture);

                resourceLayoutElementDescriptionList.Add(
                    new ResourceLayoutElementDescription(tex2d.TextureName, ResourceKind.TextureReadOnly,
                        ShaderStages.Fragment)
                );
                resourceLayoutElementDescriptionList.Add(
                    new ResourceLayoutElementDescription(tex2d.SamplerName, ResourceKind.Sampler,
                        ShaderStages.Fragment)
                );

                bindableResourceList.Add(textureView);
                bindableResourceList.Add(graphicsDevice.Aniso4xSampler);
            }

            foreach (var uniform in PipelineState.UniformList)
            {
                uniform.ConfigureDeviceBuffers(graphicsDevice, resourceFactory);

                resourceLayoutElementDescriptionList.Add(uniform.ResourceLayoutElementDescription);

                bindableResourceList.Add(uniform.DeviceBufferRange);

                if (uniform.ResourceLayoutElementDescription.Options == ResourceLayoutElementOptions.DynamicBinding)
                {
                    ri.UniformStrides.Add(uniform.DeviceBufferRange.SizeInBytes);
                }
            }

            ri.ResourceLayout = resourceFactory.CreateResourceLayout(
                new ResourceLayoutDescription(resourceLayoutElementDescriptionList.ToArray()));

            ri.ResourceSet = resourceFactory.CreateResourceSet(
                new ResourceSetDescription(
                    ri.ResourceLayout,
                    bindableResourceList.ToArray()
                )
            );

            pd.BlendState = PipelineState.BlendStateDescription;
            pd.DepthStencilState = PipelineState.DepthStencilState;
            pd.RasterizerState = PipelineState.RasterizerStateDescription;

            // TODO - cache based on the shader description and reuse shader objects
            if (null != PipelineState.VertexShaderDescription && null != PipelineState.FragmentShaderDescription)
            {
                Shader[] shaders = resourceFactory.CreateFromSpirv(
                    PipelineState.VertexShaderDescription.Value,
                    PipelineState.FragmentShaderDescription.Value,
                    GetOptions(graphicsDevice, framebuffer)
                );

                Shader vs = shaders[0];
                Shader fs = shaders[1];

                pd.ShaderSet = new ShaderSetDescription(
                    vertexLayouts: new VertexDeclaration[] { VertexLayout },
                    shaders: new Shader[] { vs, fs });
            }

            pd.ResourceLayouts = new[] { vpLayout, ri.ResourceLayout };

            pd.Outputs = framebuffer.OutputDescription;
            */
            ri.GraphicsDevice = graphicsDevice; //resourceFactory.CreateGraphicsPipeline(pd);

            RenderInfoCache.Add(graphicsDevice, ri);

            return ri;
        }
        /*
        private static CrossCompileOptions GetOptions(GraphicsDevice gd, Framebuffer framebuffer)
        {
            SpecializationConstant[] specializations = GetSpecializations(gd, framebuffer);

            bool fixClipZ = (gd.BackendType == GraphicsBackend.OpenGL || gd.BackendType == GraphicsBackend.OpenGLES)
                            && !gd.IsDepthRangeZeroToOne;

            bool invertY = false;

            return new CrossCompileOptions(fixClipZ, invertY, specializations);
        }

        public static SpecializationConstant[] GetSpecializations(GraphicsDevice gd, Framebuffer framebuffer)
        {
            bool glOrGles = gd.BackendType == GraphicsBackend.OpenGL || gd.BackendType == GraphicsBackend.OpenGLES;

            List<SpecializationConstant> specializations = new List<SpecializationConstant>();
            specializations.Add(new SpecializationConstant(100, gd.IsClipSpaceYInverted));
            specializations.Add(new SpecializationConstant(101, glOrGles)); // TextureCoordinatesInvertedY
            specializations.Add(new SpecializationConstant(102, gd.IsDepthRangeZeroToOne));

            PixelFormat swapchainFormat = framebuffer.OutputDescription.ColorAttachments[0].Format;
            bool swapchainIsSrgb = swapchainFormat == PixelFormat.B8_G8_R8_A8_UNorm_SRgb
                                   || swapchainFormat == PixelFormat.R8_G8_B8_A8_UNorm_SRgb;
            specializations.Add(new SpecializationConstant(103, false));

            return specializations.ToArray();
        }*/

        public void ReleaseUnmanagedResources()
        {
            /*foreach (var entry in RenderInfoCache)
            {
                var key = entry.Key;
                var ri = entry.Value;

                var f = key.Item2 as DisposeCollectorResourceFactory;
                f.DisposeCollector.Remove(ri.GraphicsDevice);
                f.DisposeCollector.Remove(ri.ResourceLayout);
                f.DisposeCollector.Remove(ri.ResourceSet);
                f.DisposeCollector.Remove(ri.ModelViewBuffer);

                ri.GraphicsDevice.Dispose();
                ri.ResourceLayout.Dispose();
                ri.ResourceSet.Dispose();
                ri.ModelViewBuffer.Dispose();
            }*/
        }
    }


}


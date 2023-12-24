using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph
{
    public interface IPipelineState
    {
        ShaderDescription? VertexShaderDescription { get; set; }
        ShaderDescription? FragmentShaderDescription { get; set; }
        IReadOnlyList<ITexture2D> TextureList { get; }
        IReadOnlyList<IBindable> UniformList { get; }
        BlendState BlendStateDescription { get; set; }
        DepthStencilState DepthStencilState { get; set; }
        RasterizerState RasterizerStateDescription { get; set; }
        void AddTexture(ITexture2D texture);
        void AddUniform(IBindable uniform);
        void AddUniform(IDrawable drawable, IBindable uniform);
    }
}
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph;

public class PipelineState : IPipelineState
{
    public ShaderDescription? VertexShaderDescription { get; set; }
    public ShaderDescription? FragmentShaderDescription { get; set; }

    private readonly List<ITexture2D> _textureList = new List<ITexture2D>();
    public IReadOnlyList<ITexture2D> TextureList => _textureList;

    private readonly List<IBindable> _uniformList = new List<IBindable>();
    public IReadOnlyList<IBindable> UniformList => _uniformList;

    private readonly Dictionary<IDrawable, IBindable> _uniformDictionary = new Dictionary<IDrawable, IBindable>();
    public Dictionary<IDrawable, IBindable> UniformDictionary => _uniformDictionary;

    public BlendState BlendStateDescription { get; set; } = BlendState.AlphaBlend; //SingleOverrideBlend;

    public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;

    public RasterizerState RasterizerStateDescription { get; set; } = RasterizerState.CullNone;

    public void AddTexture(ITexture2D texture)
    {
        _textureList.Add(texture);
    }

    public void AddUniform(IBindable uniform)
    {
        _uniformList.Add(uniform);
    }

    public void AddUniform(IDrawable drawable, IBindable uniform)
    {
        _uniformDictionary.Add(drawable, uniform);
    }
}
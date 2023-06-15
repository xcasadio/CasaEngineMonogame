using System.ComponentModel;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game;

public class GraphicsSettings
{
    public int MultiSampleQuality { get; set; }

    //spritebatch
    private const string Spritebatch = "SpriteBatch";
    [Category(Spritebatch)]
    public SpriteSortMode SpriteSortMode { get; set; } = SpriteSortMode.BackToFront;

    [Category(Spritebatch)]
    public BlendState BlendState { get; set; } = BlendState.AlphaBlend;

    [Category(Spritebatch)]
    public SamplerState SamplerState { get; set; } = SamplerState.PointClamp;

    [Category(Spritebatch)]
    public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;

    [Category(Spritebatch)]
    public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;
}
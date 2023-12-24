using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph
{
    public interface ITexture2D
    {
        Texture ProcessedTexture { get; }
        uint ResourceSetNo { get; set; }
        string TextureName { get; set; }
        string SamplerName { get; set; }
    }
}
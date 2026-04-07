using Microsoft.Xna.Framework.Graphics;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Materials.Compilation;

public enum CompiledMaterialTextureBindingKind
{
    Texture2D,
    TextureCube,
}

public readonly struct CompiledMaterialTextureBinding
{
    public CompiledMaterialTextureBinding(
        Guid assetId,
        CompiledMaterialTextureBindingKind kind,
        Texture2D? texture = null,
        XnaTextureCube? textureCube = null)
    {
        if (kind == CompiledMaterialTextureBindingKind.Texture2D && textureCube is not null)
        {
            throw new ArgumentException("A 2D texture binding cannot carry a texture cube.", nameof(textureCube));
        }

        if (kind == CompiledMaterialTextureBindingKind.TextureCube && texture is not null)
        {
            throw new ArgumentException("A texture-cube binding cannot carry a 2D texture.", nameof(texture));
        }

        AssetId = assetId;
        Kind = kind;
        Texture = texture;
        TextureCube = textureCube;
    }

    public Guid AssetId { get; }

    public CompiledMaterialTextureBindingKind Kind { get; }

    public Texture2D? Texture { get; }

    public XnaTextureCube? TextureCube { get; }
}
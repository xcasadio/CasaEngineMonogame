using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Materials;

public class MaterialTexture : MaterialAsset
{
    public long TextureAssetId { get; set; }
    public Texture Texture;

    public override IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> GetVertexPropertiesNeeded()
    {
        yield return new Tuple<VertexElementFormat, VertexElementUsage>(VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate);
    }

    //Add Variables
    public string GetConstant()
    {
        return "DECLARE_TEXTURE(Texture, 0);";
    }

    public string GetVertexComputation()
    {
        return "vout.TextureCoordinate0 = vin.TextureCoordinate0;";
    }

    public string GetTextureColorFromUv()
    {
        return "SAMPLE_TEXTURE(Texture, pin.TextureCoordinate0);";
    }

    public override void Accept(IMaterialAssetVisitor visitor)
    {
        visitor.Visit(this);
    }
}
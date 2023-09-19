using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraphNodeTextureUV : MaterialGraphNode
{
    public override IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> AddVertexPropertiesNeeded()
    {
        yield return new Tuple<VertexElementFormat, VertexElementUsage>(VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate);
    }

    public override string AddVertexComputation()
    {
        return "vout.TextureCoordinate0 = vin.TextureCoordinate0;";
    }

    public override string GetPixelComputation()
    {
        return "pin.TextureCoordinate0";
    }
}
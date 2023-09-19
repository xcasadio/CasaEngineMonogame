using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials;

public abstract class MaterialAsset : IMaterialAssetVisitable
{
    public abstract void Accept(IMaterialAssetVisitor visitor);

    public virtual IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> GetVertexPropertiesNeeded()
    {
        return Enumerable.Empty<Tuple<VertexElementFormat, VertexElementUsage>>();
    }
}

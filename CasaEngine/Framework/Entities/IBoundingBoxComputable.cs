using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities;

public interface IBoundingBoxComputable
{
#if EDITOR
    BoundingBox BoundingBox { get; }
#endif
}
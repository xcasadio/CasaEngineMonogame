using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Design;

public interface IBoundingBoxComputable
{
#if EDITOR
    BoundingBox BoundingBox { get; }
#endif
}
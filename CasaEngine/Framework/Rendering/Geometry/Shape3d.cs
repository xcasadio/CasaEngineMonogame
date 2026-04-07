using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Geometry;

public abstract class Shape3d : ObjectBase
{
    public Shape3dType Type { get; }

    public abstract BoundingBox BoundingBox { get; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }
}
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Objects;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Shapes;

public abstract class Shape3d : ObjectBase
{
    public Shape3dType Type { get; }

    public abstract BoundingBox BoundingBox { get; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("shape_type", Type.ConvertToString());
    }

#endif
}
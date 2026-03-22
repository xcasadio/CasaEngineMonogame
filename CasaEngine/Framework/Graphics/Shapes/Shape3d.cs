using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics.Shapes;

public abstract class Shape3d : ObjectBase
{
    public Shape3dType Type { get; }

    public abstract BoundingBox BoundingBox { get; }

    protected Shape3d(Shape3dType type)
    {
        Type = type;
    }

    public override void Save(JObject jObject)
    {
        throw new NotSupportedException($"{GetType().Name} authoring serialization lives in CasaEngine.EditorServices.");
    }
}
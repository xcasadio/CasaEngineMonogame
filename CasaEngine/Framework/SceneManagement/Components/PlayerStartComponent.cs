using System.ComponentModel;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Player Start")]
public class PlayerStartComponent : SceneComponent
{
    public PlayerStartComponent() : base()
    {
    }

    public PlayerStartComponent(PlayerStartComponent other) : base(other)
    {
    }

    public override PlayerStartComponent Clone()
    {
        return new PlayerStartComponent(this);
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.Transform(-Vector3.One / 2f, WorldMatrixWithScale);
        var max = Vector3.Transform(Vector3.One / 2f, WorldMatrixWithScale);

        return new BoundingBox(min, max);
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }


#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
    }

#endif
}
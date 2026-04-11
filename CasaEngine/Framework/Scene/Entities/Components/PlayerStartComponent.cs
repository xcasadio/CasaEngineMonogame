using System.ComponentModel;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

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
        var localBounds = new BoundingBox(-Vector3.One / 2f, Vector3.One / 2f);
        return localBounds.Transform(WorldMatrixWithScale);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
    }
}
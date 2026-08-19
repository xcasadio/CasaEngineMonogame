using System.ComponentModel;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Player Start")]
public class PlayerStartComponent : SceneComponent
{
    public PlayerIndex PlayerIndex { get; set; } = PlayerIndex.One;

    public PlayerStartComponent() : base()
    {
    }

    public PlayerStartComponent(PlayerStartComponent other) : base(other)
    {
        PlayerIndex = other.PlayerIndex;
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

        if (element.ContainsKey("player_index"))
        {
            PlayerIndex = (PlayerIndex)element["player_index"].GetInt32();
        }
    }
}
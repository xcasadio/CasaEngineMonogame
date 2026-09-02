
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteData : ObjectBase
{
    public SpriteData()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public SpriteData(Guid id) : base(id)
    {
    }

    public Guid SpriteSheetAssetId { get; set; }
    public Rectangle PositionInTexture { get; set; }
    public Point Origin { get; set; }
    public List<Socket> Sockets { get; } = new();
    public List<Collision2d> CollisionShapes { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        SpriteSheetAssetId = element["sprite_sheet_asset_id"].GetGuid();
        PositionInTexture = element["location"].GetRectangle();
        Origin = element["hotspot"].GetPoint();

        if (element.TryGetValue("collisions", out var collisionsElement))
        {
            foreach (var collisionElement in collisionsElement)
            {
                var collision2d = new Collision2d();
                collision2d.Load((JObject)collisionElement);
                CollisionShapes.Add(collision2d);
            }
        }

        if (element.TryGetValue("sockets", out var socketsElement))
        {
            foreach (var socketElement in socketsElement)
            {
                var socket = new Socket();
                socket.Load((JObject)socketElement);
                Sockets.Add(socket);
            }
        }
    }
}

using CasaEngine.Core.Logging;
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

    /// <summary>
    /// The PSX semi-transparency mode of the sprite (ADR-0051). <see cref="SpritePsxSemiTransparency.None"/> by default and
    /// when the serialized field is absent.
    /// </summary>
    public SpritePsxSemiTransparency PsxSemiTransparency { get; set; }
    public List<Socket> Sockets { get; } = new();
    public List<Collision2d> CollisionShapes { get; } = new();

    /// <summary>The key of <see cref="PsxSemiTransparency"/> in the serialized sprite: the member name, absent for <c>None</c>.</summary>
    public const string PsxSemiTransparencyKey = "psx_semi_transparency";

    private string FileOrName => string.IsNullOrEmpty(FileName) ? Name : FileName;

    private SpritePsxSemiTransparency ReadPsxSemiTransparency(JObject element)
    {
        var text = element[PsxSemiTransparencyKey]?.Value<string>();
        if (string.IsNullOrEmpty(text))
        {
            return SpritePsxSemiTransparency.None;
        }

        if (Enum.TryParse<SpritePsxSemiTransparency>(text, out var mode) && Enum.IsDefined(mode))
        {
            return mode;
        }

        Logs.WriteError($"SpriteData '{FileOrName}': unknown {PsxSemiTransparencyKey} '{text}', the sprite is not semi-transparent");
        return SpritePsxSemiTransparency.None;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        SpriteSheetAssetId = element["sprite_sheet_asset_id"].GetGuid();
        PositionInTexture = element["location"].GetRectangle();
        Origin = element["hotspot"].GetPoint();
        PsxSemiTransparency = ReadPsxSemiTransparency(element);

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
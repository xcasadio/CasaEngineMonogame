using System.Text.Json;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities.Components;

public class TileComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.Tile;

    public Tile Tile { get; set; }

    public TileComponent(Entity entity) : base(entity, ComponentId)
    { }

    public override void Initialize(CasaEngineGame game)
    {
        Tile.Initialize();
    }

    public override void Update(float elapsedTime)
    {
        Tile.Update(elapsedTime);
    }

    public override void Draw()
    {
        var translation = Owner.Coordinates.WorldMatrix.Translation;
        Tile.Draw(translation.X, translation.Y, translation.Z);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
};
using System.Text.Json;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

public class StaticSpriteComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.StaticSprite;

    private string? _spriteId;
    private Sprite? _sprite;
    private SpriteData? _spriteData;
    private CasaEngineGame _game;
    private Renderer2dComponent? _renderer2dComponent;

    public string? SpriteId
    {
        get => _spriteId;
        set
        {
            _spriteId = value;
            if (_spriteId != null)
            {
                _spriteData = _game.GameManager.AssetContentManager.GetAsset<SpriteData>(_spriteId);
                _sprite = Sprite.Create(_spriteData, _game.GameManager.AssetContentManager);
            }
        }
    }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    public StaticSpriteComponent(Entity entity) : base(entity, ComponentId)
    { }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _renderer2dComponent = _game.GetGameComponent<Renderer2dComponent>();
    }

    public override void Update(float elapsedTime)
    {

    }

    public override void Draw()
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        var worldMatrix = Owner.Coordinates.WorldMatrix;
        _renderer2dComponent.AddSprite(_sprite.Texture.Resource, //TODO : load all sprites in a dictionary<name, sprite>
            _spriteData.PositionInTexture,
            _spriteData.Origin,
            new Vector2(worldMatrix.Translation.X, worldMatrix.Translation.Y),
            0.0f,
            new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y),
            Color.White,
            Owner.Coordinates.Position.Z,
            SpriteEffects.None);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
};
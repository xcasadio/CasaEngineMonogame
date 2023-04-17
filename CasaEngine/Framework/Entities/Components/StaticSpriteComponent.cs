using System.Text.Json;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

public class StaticSpriteComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.StaticSprite;

    private string _spriteId;
    private Sprite _sprite;
    //SpriteRenderer _spriteRenderer;

    public string SpriteId
    {
        get => _spriteId;
        set
        {
            _spriteId = value;
            //_sprite = new Sprite(*Game::Instance().GetAssetManager().GetAsset<SpriteData>(_spriteId));
        }
    }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    public StaticSpriteComponent(Entity entity) : base(entity, ComponentId)
    { }

    public override void Initialize(CasaEngineGame game)
    {
        //_spriteRenderer = Game::Instance().GetGameComponent<SpriteRenderer>();
    }

    public override void Update(float elapsedTime)
    {

    }

    public override void Draw()
    {
        //_spriteRenderer->AddSprite(_sprite, GetEntity()->GetCoordinates().GetWorldMatrix(), _color, _spriteEffect);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
};
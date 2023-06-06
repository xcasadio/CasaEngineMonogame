using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public class StaticSpriteComponent : Component, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.StaticSprite;

    private Sprite? _sprite;
    private SpriteData? _spriteData;
    private CasaEngineGame _game;
    private Renderer2dComponent? _renderer2dComponent;

    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; }

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
        //update collision objects
    }

    public override void Draw()
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        var worldMatrix = Owner.Coordinates.WorldMatrix;
        _renderer2dComponent.DrawSprite(_sprite,
            _spriteData,
            new Vector2(worldMatrix.Translation.X, worldMatrix.Translation.Y),
            0.0f,
            new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y),
            Color.White,
            Owner.Coordinates.Position.Z);
    }

    public void OnHit(Collision collision)
    {

    }

    public void OnHitEnded(Collision collision)
    {

    }

    public override void Load(JsonElement element)
    {
        var spriteDataName = element.GetProperty("spriteDataName").GetString();

        if (!string.Equals(spriteDataName, "null", StringComparison.CurrentCultureIgnoreCase))
        {
            LoadSpriteData(spriteDataName);
        }
    }

    private void LoadSpriteData(string? spriteDataName)
    {
        _spriteData = _game.GameManager.AssetContentManager.GetAsset<SpriteData>(spriteDataName);
        _sprite = Sprite.Create(_spriteData, _game.GameManager.AssetContentManager);

        var physicsEngineComponent = _game.GetGameComponent<PhysicsEngineComponent>();
        var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(_spriteData, Owner, physicsEngineComponent, this);
        if (collisionObject != null)
        {
            //_collisionObjectByFrameId[frame.SpriteId] = collisionObject;
            physicsEngineComponent.AddCollisionObject(collisionObject);
        }
    }

#if EDITOR

    public void TryLoadSpriteData(string? spriteDataName)
    {
        if (spriteDataName == null)
        {
            return;
        }

        LoadSpriteData(spriteDataName);
    }

    public override void Save(JObject jObject)
    {
        jObject.Add("spriteDataName", _spriteData == null ? "null" : _spriteData.Name);
        base.Save(jObject);
    }

#endif
}
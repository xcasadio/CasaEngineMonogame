using System.ComponentModel;
using System.Text.Json;
using BulletSharp;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
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
    private SpriteRendererComponent? _spriteRendererComponent;
    private readonly List<(Shape2d, CollisionObject)> _collisionObjects = new();

    [Browsable(false)]
    public PhysicsType PhysicsType { get; }

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    public StaticSpriteComponent(Entity entity) : base(entity, ComponentId)
    { }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _spriteRendererComponent = _game.GetGameComponent<SpriteRendererComponent>();
    }

    public override void Update(float elapsedTime)
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            Physics2dHelper.UpdateBodyTransformation(Owner, collisionObject, shape2d, _spriteData.Origin, _spriteData.PositionInTexture);
        }
    }

    public override void Draw()
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        var worldMatrix = Owner.Coordinates.WorldMatrix;
        _spriteRendererComponent.DrawSprite(_sprite,
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

    public override Component Clone(Entity owner)
    {
        var component = new StaticSpriteComponent(owner);

        component._spriteData = _spriteData;

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
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

        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            physicsEngineComponent.RemoveCollisionObject(collisionObject);
        }

        foreach (var collisionShape in _spriteData.CollisionShapes)
        {
            var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, Owner, physicsEngineComponent, this, color);
            if (collisionObject != null)
            {
                Physics2dHelper.UpdateBodyTransformation(Owner, collisionObject, collisionShape.Shape, _spriteData.Origin, _spriteData.PositionInTexture);
                physicsEngineComponent.AddCollisionObject(collisionObject);
                _collisionObjects.Add(new(collisionShape.Shape, collisionObject));
            }
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

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("spriteDataName", _spriteData == null ? "null" : _spriteData.AssetInfo.Name);
        base.Save(jObject, option);
    }

#endif
}
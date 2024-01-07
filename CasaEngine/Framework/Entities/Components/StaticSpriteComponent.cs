using System.ComponentModel;
using System.Text.Json;
using BulletSharp;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public class StaticSpriteComponent : Component, ICollideableComponent, IComponentDrawable
{
    public override int ComponentId => (int)ComponentIds.StaticSprite;

    private Sprite? _sprite;
    private SpriteData? _spriteData;
    private SpriteRendererComponent? _spriteRendererComponent;
    private readonly List<(Shape2d, CollisionObject)> _collisionObjects = new();
    private PhysicsEngineComponent? _physicsEngineComponent;

    [Browsable(false)]
    public PhysicsType PhysicsType { get; }

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    public StaticSpriteComponent() : base()
    { }

    public override void Initialize(Entity entity)
    {
        base.Initialize(entity);

        _spriteRendererComponent = Owner.Game.GetGameComponent<SpriteRendererComponent>();
        _physicsEngineComponent = Owner.Game.GetGameComponent<PhysicsEngineComponent>();
    }

    public override void Update(float elapsedTime)
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            Physics2dHelper.UpdateBodyTransformation(Owner, collisionObject, shape2d, _spriteData.Origin, _spriteData.PositionInTexture);
        }
    }

    public BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (_spriteData != null)
        {
            var x = (float)_spriteData.PositionInTexture.X - _spriteData.Origin.X;
            var y = (float)_spriteData.PositionInTexture.Y - _spriteData.Origin.Y;
            var halfWidth = _spriteData.PositionInTexture.Width / 2f;
            var halfHeight = _spriteData.PositionInTexture.Height / 2f;

            min = Vector3.Min(min, new Vector3(x - halfWidth, y - halfHeight, 0f));
            max = Vector3.Max(max, new Vector3(x + halfWidth, y + halfHeight, 0f));
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        min = Vector3.Transform(min, Owner.Coordinates.WorldMatrix);
        max = Vector3.Transform(max, Owner.Coordinates.WorldMatrix);

        return new BoundingBox(min, max);
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

    public override void OnEnabledValueChange()
    {
        if (Owner.IsEnabled)
        {
            AddCollisions();
        }
        else
        {
            RemoveCollisions();
        }
    }

    public void OnHit(Collision collision)
    {

    }

    public void OnHitEnded(Collision collision)
    {

    }

    public override Component Clone()
    {
        var component = new StaticSpriteComponent();

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
        _spriteData = Owner.Game.GameManager.AssetContentManager.GetAsset<SpriteData>(spriteDataName);
        _sprite = Sprite.Create(_spriteData, Owner.Game.GameManager.AssetContentManager);
        RemoveCollisions();
        AddCollisions();
    }
    private void AddCollisions()
    {
        foreach (var collisionShape in _spriteData.CollisionShapes)
        {
            var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, Owner, _physicsEngineComponent, this, color);
            if (collisionObject != null)
            {
                Physics2dHelper.UpdateBodyTransformation(Owner, collisionObject, collisionShape.Shape, _spriteData.Origin, _spriteData.PositionInTexture);
                _physicsEngineComponent.AddCollisionObject(collisionObject);
                _collisionObjects.Add(new(collisionShape.Shape, collisionObject));
            }
        }
    }

    public void RemoveCollisions()
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            _physicsEngineComponent.RemoveCollisionObject(collisionObject);
            _physicsEngineComponent.ClearCollisionDataOf(this);
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
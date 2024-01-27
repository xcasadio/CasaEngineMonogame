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

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Static Sprite")]
public class StaticSpriteComponent : SceneComponent, ICollideableComponent, IComponentDrawable, IBoundingBoxable
{
    private Sprite? _sprite;
    private SpriteData? _spriteData;
    private SpriteRendererComponent? _spriteRendererComponent;
    private readonly List<(Shape2d, CollisionObject)> _collisionObjects = new();
    private PhysicsEngineComponent? _physicsEngineComponent;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    public StaticSpriteComponent()
    {

    }

    public StaticSpriteComponent(StaticSpriteComponent other) : base(other)
    {
        _spriteData = other._spriteData;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _spriteRendererComponent = World.Game.GetGameComponent<SpriteRendererComponent>();
        _physicsEngineComponent = World.Game.GetGameComponent<PhysicsEngineComponent>();
    }

    public override StaticSpriteComponent Clone()
    {
        return new StaticSpriteComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            Physics2dHelper.UpdateBodyTransformation(WorldPosition, WorldRotation, WorldScale,
                collisionObject, shape2d, _spriteData.Origin, _spriteData.PositionInTexture);
        }
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (_spriteData != null)
        {
            var halfWidth = _spriteData.PositionInTexture.Width / 2f;
            var halfHeight = _spriteData.PositionInTexture.Height / 2f;

            min = Vector3.Min(min, new Vector3(-halfWidth, -halfHeight, 0f));
            max = Vector3.Max(max, new Vector3(halfWidth, halfHeight, 0.1f));
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        min = Vector3.Transform(min, WorldMatrixWithScale);
        max = Vector3.Transform(max, WorldMatrixWithScale);

        return new BoundingBox(min, max);
    }

    public override void Draw(float elapsedTime)
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        _spriteRendererComponent.DrawSprite(_sprite,
            new Vector2(WorldPosition.X, WorldPosition.Y),
            0.0f,
            new Vector2(WorldScale.X, WorldScale.Y),
            Color.White,
            WorldPosition.Z);
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
        _spriteData = World.Game.AssetContentManager.GetAsset<SpriteData>(spriteDataName);
        _sprite = Sprite.Create(_spriteData, World.Game.AssetContentManager);
        RemoveCollisions();
        AddCollisions();
        IsBoundingBoxDirty = true;
    }

    private void AddCollisions()
    {
        foreach (var collisionShape in _spriteData.CollisionShapes)
        {
            var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, WorldMatrixWithScale,
                _physicsEngineComponent, this, color);
            if (collisionObject != null)
            {
                Physics2dHelper.UpdateBodyTransformation(WorldPosition, WorldRotation, WorldScale,
                    collisionObject, collisionShape.Shape, _spriteData.Origin, _spriteData.PositionInTexture);
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
            _physicsEngineComponent.ClearCollisionDataFrom(this);
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
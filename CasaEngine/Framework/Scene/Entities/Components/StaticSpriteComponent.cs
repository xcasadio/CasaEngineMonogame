using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Static Sprite")]
public class StaticSpriteComponent : SceneComponent, ICollideableComponent, IComponentDrawable, IBoundingBoxable
{
    private Sprite _sprite;
    private SpriteData _spriteData;
    private SpriteRendererComponent _spriteRendererComponent;
    private DepthSortable2DComponent _depthSortable2DComponent;
    private readonly List<(Shape2d, PhysicsBody)> _collisionObjects = new();
    private IPhysicsWorld _physicsWorldContext;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; }

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    /// <summary>
    /// Optional asset ID used to pre-configure the sprite when creating an entity
    /// via drag-and-drop. Loaded during <see cref="InitializeWithWorld"/>.
    /// </summary>
    public Guid SpriteAssetId { get; set; } = Guid.Empty;

    public StaticSpriteComponent()
    {

    }

    public StaticSpriteComponent(StaticSpriteComponent other) : base(other)
    {
        _spriteData = other._spriteData;
        SpriteAssetId = other.SpriteAssetId;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        _spriteRendererComponent = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
    _depthSortable2DComponent = Owner.GetComponent<DepthSortable2DComponent>();
        _physicsWorldContext = Owner.World.PhysicsWorld;

        if (SpriteAssetId != Guid.Empty && _spriteData == null)
        {
            var spriteData = Owner.World.Game.AssetContentManager.GetAsset<SpriteData>(SpriteAssetId);
            if (spriteData != null)
            {
                _spriteData = spriteData;
                _sprite = Sprite.Create(_spriteData, Owner.World.Game.AssetContentManager);
                AddCollisions();
                IsBoundingBoxDirty = true;
            }
        }
    }

    public override StaticSpriteComponent Clone()
    {
        return new StaticSpriteComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            Physics2dHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                collisionObject, shape2d, _spriteData.Origin, _spriteData.PositionInTexture);
        }

        base.Update(elapsedTime);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (_spriteData != null)
        {
            return SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData).Transform(WorldMatrixWithScale);
        }

        const float length = 0.5f;
        return new BoundingBox(Vector3.One * -length, Vector3.One * length).Transform(WorldMatrixWithScale);
    }

    public override void Draw(float elapsedTime)
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        var position = new Vector2(Position.X, Position.Y);
        var scale = new Vector2(Scale.X, Scale.Y);
        if (_depthSortable2DComponent != null)
        {
            var sortKey = _depthSortable2DComponent.BuildSortKey(Position, Owner.World.CurrentRenderFrame);
            _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color.White, Position.Z, sortKey);
            return;
        }

        _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color.White, Position.Z);
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (Owner.IsEnabled)
        {
            AddCollisions();
        }
        else
        {
            RemoveCollisions();
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var spriteDataName = element["spriteDataName"].GetString();

        if (!string.Equals(spriteDataName, "null", StringComparison.CurrentCultureIgnoreCase))
        {
            LoadSpriteData(spriteDataName);
        }
    }

    private void LoadSpriteData(string spriteDataName)
    {
        _spriteData = Owner.World.Game.AssetContentManager.GetAsset<SpriteData>(spriteDataName);
        if (_spriteData != null && _spriteData.AssetId != Guid.Empty)
        {
            SpriteAssetId = _spriteData.AssetId;
        }

        _sprite = Sprite.Create(_spriteData, Owner.World.Game.AssetContentManager);
        RemoveCollisions();
        AddCollisions();
        IsBoundingBoxDirty = true;
    }

    public bool ReloadSpriteAsset(Guid spriteAssetId, SpriteData spriteData)
    {
        ArgumentNullException.ThrowIfNull(spriteData);

        bool isMatchingSprite = SpriteAssetId == spriteAssetId
            || (_spriteData != null
                && (_spriteData.AssetId == spriteAssetId
                    || _spriteData.Id == spriteAssetId
                    || string.Equals(_spriteData.Name, spriteData.Name, StringComparison.Ordinal)));
        if (!isMatchingSprite)
        {
            return false;
        }

        _spriteData = spriteData;
        if (spriteData.AssetId != Guid.Empty)
        {
            SpriteAssetId = spriteData.AssetId;
        }

        RemoveCollisions();

        var assetContentManager = Owner?.World?.Game?.AssetContentManager;
        if (assetContentManager != null)
        {
            _sprite = Sprite.Create(spriteData, assetContentManager);
        }

        if (Owner?.IsEnabled != false)
        {
            AddCollisions();
        }

        IsBoundingBoxDirty = true;
        return true;
    }

    private void AddCollisions()
    {
        foreach (var collisionShape in _spriteData.CollisionShapes)
        {
            var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, LocalScale, WorldMatrixNoScale, _physicsWorldContext, this, color);
            if (collisionObject != null)
            {
                Physics2dHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                    collisionObject, collisionShape.Shape, _spriteData.Origin, _spriteData.PositionInTexture);
                _physicsWorldContext.AddCollisionObject(collisionObject);
                _collisionObjects.Add(new(collisionShape.Shape, collisionObject));
            }
        }
    }

    public void RemoveCollisions()
    {
        foreach (var (shape2d, collisionObject) in _collisionObjects)
        {
            _physicsWorldContext.RemoveCollisionObject(collisionObject);
            _physicsWorldContext.ClearCollisionDataFrom(this);
        }
    }

    public void TryLoadSpriteData(string spriteDataName)
    {
        if (spriteDataName == null)
        {
            return;
        }

        LoadSpriteData(spriteDataName);
    }

}
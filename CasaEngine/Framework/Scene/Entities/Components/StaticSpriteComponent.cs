using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
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

    // ADR-0037: the sprite data is a shared asset, held through a counted handle for as long as the
    // component references it and given back in ReleaseSpriteAssetHandles (InitializeWithWorld's reset,
    // a reload, and Detach). The Sprite itself is disposed there too: it holds the sheet texture.
    private AssetHandle<SpriteData> _spriteDataHandle;
    private SpriteRendererComponent _spriteRendererComponent;
    private DepthSortable2DComponent _depthSortable2DComponent;
    private readonly List<(Collision2d, PhysicsBody)> _collisionObjects = new();
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
            _spriteDataHandle = Owner.World.Game.AssetContentManager.Acquire<SpriteData>(SpriteAssetId);
            _spriteData = _spriteDataHandle.Asset;
            _sprite = Sprite.Create(_spriteData, Owner.World.Game.AssetContentManager);
            AddCollisions();
            IsBoundingBoxDirty = true;
        }
    }

    public override void Detach()
    {
        ReleaseSpriteAssetHandles();
        base.Detach();
    }

    /// <summary>Gives back the sprite data hold and disposes the sprite (its sheet texture hold with it).</summary>
    private void ReleaseSpriteAssetHandles()
    {
        _sprite?.Dispose();
        _sprite = null;
        _spriteDataHandle?.Dispose();
        _spriteDataHandle = null;
    }

    public override StaticSpriteComponent Clone()
    {
        return new StaticSpriteComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        foreach (var (collision2d, collisionObject) in _collisionObjects)
        {
            SpriteCollisionHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                collisionObject, collision2d, _spriteData.Origin, _spriteData.PositionInTexture);
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

    /// <summary>Resolves a sprite data by its catalog name, then holds it through a handle (P6).</summary>
    private void LoadSpriteData(string spriteDataName)
    {
        var assetContentManager = Owner.World.Game.AssetContentManager;

        ReleaseSpriteAssetHandles();

        var assetInfo = AssetCatalog.Get(spriteDataName);
        if (assetInfo != null)
        {
            _spriteDataHandle = assetContentManager.Acquire<SpriteData>(assetInfo.Id);
            _spriteData = _spriteDataHandle.Asset;
            if (_spriteData.AssetId != Guid.Empty)
            {
                SpriteAssetId = _spriteData.AssetId;
            }
        }
        else
        {
            _spriteData = null;
        }

        _sprite = Sprite.Create(_spriteData, assetContentManager);
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

        // spriteData is supplied directly by the reload service, not resolved through Acquire here: give
        // back whatever sprite data hold this component took itself, so it does not keep pinning a stale
        // instance the manager may have already replaced.
        _spriteDataHandle?.Dispose();
        _spriteDataHandle = null;
        _spriteData = spriteData;
        if (spriteData.AssetId != Guid.Empty)
        {
            SpriteAssetId = spriteData.AssetId;
        }

        RemoveCollisions();

        var assetContentManager = Owner?.World?.Game?.AssetContentManager;
        if (assetContentManager != null)
        {
            _sprite?.Dispose();
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
            var collisionObject = SpriteCollisionHelper.CreateCollisionBody(collisionShape, LocalScale, WorldMatrixNoScale, _physicsWorldContext, this);
            if (collisionObject != null)
            {
                SpriteCollisionHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                    collisionObject, collisionShape, _spriteData.Origin, _spriteData.PositionInTexture);
                _physicsWorldContext.AddCollisionObject(collisionObject);
                _collisionObjects.Add(new(collisionShape, collisionObject));
            }
        }
    }

    public void RemoveCollisions()
    {
        foreach (var (_, collisionObject) in _collisionObjects)
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
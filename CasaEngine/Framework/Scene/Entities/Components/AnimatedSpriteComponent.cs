using System.ComponentModel;
using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.Geometry;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : SceneComponent, ICollideableComponent, IComponentDrawable, IBoundingBoxable, IConditionalEntityUpdateSource
{
    public event EventHandler<Animation2d> AnimationFinished;
    public event EventHandler<AnimationEventAsset> AnimationEventTriggered;

    private readonly Dictionary<Guid, List<(Shape2d, PhysicsBody)>> _collisionObjectsBySpriteId = new();
    private readonly List<Guid> _animationAssetIds = new();
    private readonly Dictionary<Guid, Sprite> _spriteById = new();
    private readonly Dictionary<Guid, SpriteData> _spriteDataById = new();
    private readonly List<Guid> _spriteIdsToResolve = new();
    private readonly Dictionary<Animation2d, Animation2dCompositionSampler> _compositionSamplerByAnimation = new();

    private AssetContentManager _assetContentManager;
    private IPhysicsWorld _physicsWorldContext;
    private SpriteRendererComponent _spriteRenderer;
    private DepthSortable2DComponent _depthSortable2DComponent;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d CurrentAnimation { get; private set; }
    private Animation2dCompositionSampler _currentCompositionSampler;
    private Guid _currentSpriteId;
    public Animation2dCompositionRuntimeState CurrentCompositionState => _currentCompositionSampler?.RuntimeState;
    public List<Animation2d> Animations { get; } = new();

    [Browsable(false)]
    public PhysicsType PhysicsType => PhysicsType.Kinetic;

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; } = new();

    public bool CreatePhysicsForEachFrame { get; set; } = true;

    public AnimatedSpriteComponent()
    {
        Color = Color.White;
        SpriteEffect = SpriteEffects.None;
    }

    public AnimatedSpriteComponent(AnimatedSpriteComponent other) : base(other)
    {
        Color = other.Color;
        SpriteEffect = other.SpriteEffect;
        CurrentAnimation = other.CurrentAnimation;
        Animations.AddRange(other.Animations);
        _animationAssetIds.AddRange(other._animationAssetIds);
    }

    public void SetCurrentAnimation(Animation2d anim, bool forceReset)
    {
        if (CurrentAnimation != null && CurrentAnimation.AnimationData.Name == anim.AnimationData.Name)
        {
            if (forceReset)
            {
                CurrentAnimation.Reset();
                _currentCompositionSampler?.Reset();
                UpdateCurrentSprite(true);
            }

            return;
        }

        if (CurrentAnimation != null)
        {
            RemoveCollisionsFromSprite(_currentSpriteId);
        }

        CurrentAnimation = anim;
        CurrentAnimation.Reset();
        _compositionSamplerByAnimation.TryGetValue(CurrentAnimation, out _currentCompositionSampler);
        _currentCompositionSampler?.Reset();

        _currentSpriteId = Guid.Empty;
        UpdateCurrentSprite(true);
    }

    public void SetCurrentAnimation(int index, bool forceReset)
    {
        SetCurrentAnimation(Animations[index], forceReset);
    }

    public bool SetCurrentAnimation(string name, bool forceReset)
    {
        foreach (var anim in Animations)
        {
            if (anim.AnimationData.Name == name)
            {
                SetCurrentAnimation(anim, forceReset);
                return true;
            }
        }

        return false;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        _spriteRenderer = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
        _depthSortable2DComponent = Owner.GetComponent<DepthSortable2DComponent>();
        _assetContentManager = Owner.World.Game.AssetContentManager;
        _physicsWorldContext = Owner.World.PhysicsWorld;

        Animations.Clear();
        _compositionSamplerByAnimation.Clear();
        _currentCompositionSampler = null;
        _currentSpriteId = Guid.Empty;
        _spriteById.Clear();
        _spriteDataById.Clear();
        _collisionObjectsBySpriteId.Clear();

        foreach (var assetId in _animationAssetIds)
        {
            var animation2dData = Owner.World.Game.AssetContentManager.Load<Animation2dData>(assetId);
            var animation2d = new Animation2d(animation2dData);
            animation2d.Initialize();
            Animations.Add(animation2d);
            RegisterCompositionSampler(animation2d);
            CacheAnimationSprites(animation2dData);
        }

        if (Animations.Count > 0)
        {
            SetCurrentAnimation(0, true);
        }
    }

    public override AnimatedSpriteComponent Clone()
    {
        return new AnimatedSpriteComponent(this);
    }

    public bool ShouldUpdateWhenConditional(Entity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return CurrentAnimation != null && owner.World?.Game?.ExecutionPolicy.UpdateAnimatedSprites == true;
    }

    public override void Update(float elapsedTime)
    {
        if (!Owner.World.Game.ExecutionPolicy.UpdateAnimatedSprites)
        {
            return;
        }

        if (CurrentAnimation != null)
        {
            var wasFinished = _currentCompositionSampler?.IsFinished == true;
            var isFinished = _currentCompositionSampler?.Update(elapsedTime) == true;
            IsBoundingBoxDirty = true;
            UpdateCurrentSprite(true);
            UpdateCollisionFromSprite(_currentSpriteId);

            if (!wasFinished && isFinished)
            {
                AnimationFinished?.Invoke(this, CurrentAnimation);
            }
        }

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (CurrentAnimation != null)
        {
            if (_currentCompositionSampler != null && _currentCompositionSampler.RuntimeState.PartCount > 0)
            {
                DrawComposedAnimation();
            }
        }
    }

    public void AddAnimation(Animation2d animation2d)
    {
        animation2d.Initialize();
        Animations.Add(animation2d);
        RegisterCompositionSampler(animation2d);
        if (_assetContentManager != null)
        {
            CacheAnimationSprites(animation2d.Animation2dData);
        }
    }

    private void RegisterCompositionSampler(Animation2d animation2d)
    {
        if (!_compositionSamplerByAnimation.ContainsKey(animation2d))
        {
            var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation2d.Animation2dData));
            sampler.AnimationEventTriggered += OnAnimationEventTriggered;
            _compositionSamplerByAnimation.Add(animation2d, sampler);
        }
    }

    private void DrawComposedAnimation()
    {
        var runtimeState = _currentCompositionSampler.RuntimeState;
        var position = new Vector2(Position.X, Position.Y);
        var scale = new Vector2(Scale.X, Scale.Y);

        for (var drawIndex = 0; drawIndex < runtimeState.DrawPartIndices.Count; drawIndex++)
        {
            var part = runtimeState.Parts[runtimeState.DrawPartIndices[drawIndex]];
            if (!part.Visible)
            {
                continue;
            }

            if (!_spriteById.TryGetValue(part.SpriteId, out var sprite))
            {
                Logs.WriteError($"AnimatedSpriteComponent : the sprite of the composed part '{part.PartId}' doesn't exist '{part.SpriteId}'");
                continue;
            }

            var partPosition = new Vector2(
                position.X + part.Position.X * scale.X,
                position.Y + part.Position.Y * scale.Y);
            var spriteEffects = GetPartSpriteEffects(part);

            if (_depthSortable2DComponent != null)
            {
                var partWorldPosition = new Vector3(partPosition.X, partPosition.Y, Position.Z);
                var sortKey = BuildPartSortKey(_depthSortable2DComponent.BuildSortKey(partWorldPosition, Owner.World.CurrentRenderFrame), part);
                _spriteRenderer.DrawSprite(sprite, partPosition, 0.0f, scale, Color, Position.Z, sortKey, spriteEffects);
                continue;
            }

            var zOrder = Position.Z - part.DrawOrder * 0.0001f - part.SourceIndex * 0.000001f;
            _spriteRenderer.DrawSprite(sprite, partPosition, 0.0f, scale, Color, zOrder, spriteEffects);
        }
    }

    private SpriteEffects GetPartSpriteEffects(Animation2dPartRuntimeState part)
    {
        var spriteEffects = SpriteEffect;
        if (part.FlipX)
        {
            spriteEffects |= SpriteEffects.FlipHorizontally;
        }

        if (part.FlipY)
        {
            spriteEffects |= SpriteEffects.FlipVertically;
        }

        return spriteEffects;
    }

    private static RenderSortKey2D BuildPartSortKey(RenderSortKey2D baseSortKey, Animation2dPartRuntimeState part)
    {
        return new RenderSortKey2D(
            baseSortKey.RenderPass,
            baseSortKey.SortingLayer,
            baseSortKey.OrderInLayer,
            baseSortKey.Elevation,
            baseSortKey.SortCoordinate,
            baseSortKey.LocalSortOffset + part.DrawOrder,
            baseSortKey.StableId + part.SourceIndex);
    }

    private void CacheAnimationSprites(Animation2dData animation2dData)
    {
        _spriteIdsToResolve.Clear();
        Animation2dSpriteReferenceCollector.Collect(animation2dData, _spriteIdsToResolve);

        foreach (var spriteId in _spriteIdsToResolve)
        {
            ResolveSprite(spriteId);
        }
    }

    private Sprite ResolveSprite(Guid spriteId)
    {
        if (_spriteById.TryGetValue(spriteId, out var sprite))
        {
            return sprite;
        }

        SpriteData spriteData;
        try
        {
            spriteData = _assetContentManager.Load<SpriteData>(spriteId);
        }
        catch (InvalidOperationException exception)
        {
            Logs.WriteError($"AnimatedSpriteComponent : can't resolve sprite '{spriteId}' ({exception.Message})");
            return null;
        }

        if (spriteData == null)
        {
            Logs.WriteError($"AnimatedSpriteComponent : the sprite doesn't exist '{spriteId}'");
            return null;
        }

        sprite = Sprite.Create(spriteData, _assetContentManager);
        _spriteById.Add(spriteId, sprite);
        _spriteDataById[spriteId] = spriteData;
        CreateCollisionObjectsForSprite(spriteId, spriteData);
        return sprite;
    }

    private void CreateCollisionObjectsForSprite(Guid spriteId, SpriteData spriteData)
    {
        if (_physicsWorldContext == null
            || spriteData.CollisionShapes.Count == 0
            || _collisionObjectsBySpriteId.ContainsKey(spriteId))
        {
            return;
        }

        _collisionObjectsBySpriteId.Add(spriteId, new List<(Shape2d, PhysicsBody)>(spriteData.CollisionShapes.Count));

        foreach (var collisionShape in spriteData.CollisionShapes)
        {
            var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
            var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, LocalScale, WorldMatrixNoScale, _physicsWorldContext, this, color);
            if (collisionObject != null)
            {
                _collisionObjectsBySpriteId[spriteId].Add(new(collisionShape.Shape, collisionObject));
            }
        }
    }

    private void UpdateCurrentSprite(bool addCollision)
    {
        var spriteId = GetPrimarySpriteId();
        if (_currentSpriteId == spriteId)
        {
            if (addCollision)
            {
                AddOrUpdateCollisionFromSprite(_currentSpriteId, true);
            }

            return;
        }

        var previousSpriteId = _currentSpriteId;
        if (previousSpriteId != Guid.Empty)
        {
            RemoveCollisionsFromSprite(previousSpriteId);
        }

        _currentSpriteId = spriteId;
        IsBoundingBoxDirty = true;

        if (_currentSpriteId != Guid.Empty)
        {
            AddOrUpdateCollisionFromSprite(_currentSpriteId, addCollision);
        }
    }

    private Guid GetPrimarySpriteId()
    {
        var runtimeState = _currentCompositionSampler?.RuntimeState;
        if (runtimeState == null)
        {
            return Guid.Empty;
        }

        for (var drawIndex = 0; drawIndex < runtimeState.DrawPartIndices.Count; drawIndex++)
        {
            var part = runtimeState.Parts[runtimeState.DrawPartIndices[drawIndex]];
            if (part.Visible && part.SpriteId != Guid.Empty)
            {
                return part.SpriteId;
            }
        }

        return Guid.Empty;
    }

    private void UpdateCollisionFromSprite(Guid spriteId)
    {
        AddOrUpdateCollisionFromSprite(spriteId, false);
    }

    private void AddOrUpdateCollisionFromSprite(Guid spriteId, bool addCollision)
    {
        if (spriteId == Guid.Empty
            || !_collisionObjectsBySpriteId.TryGetValue(spriteId, out var collisionObjects)
            || !CreatePhysicsForEachFrame)
        {
            return;
        }

        var spriteData = _assetContentManager.GetAsset<SpriteData>(spriteId);

        foreach (var (shape2d, collisionObject) in collisionObjects)
        {
            Physics2dHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                collisionObject, shape2d, spriteData.Origin, spriteData.PositionInTexture);
            if (addCollision)
            {
                _physicsWorldContext.AddCollisionObject(collisionObject);
            }
        }
    }

    public void RemoveCollisionsFromSprite(Guid spriteId)
    {
        if (spriteId == Guid.Empty
            || !_collisionObjectsBySpriteId.TryGetValue(spriteId, out var collisionObjects))
        {
            return;
        }

        foreach (var (_, collisionObject) in collisionObjects)
        {
            _physicsWorldContext.RemoveCollisionObject(collisionObject);
            _physicsWorldContext.ClearCollisionDataFrom(this);
        }
    }

    private void OnAnimationEventTriggered(AnimationEventAsset animationEvent)
    {
        AnimationEventTriggered?.Invoke(this, animationEvent);
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (CurrentAnimation != null)
        {
            if (Owner.IsEnabled)
            {
                AddOrUpdateCollisionFromSprite(_currentSpriteId, true);
            }
            else
            {
                RemoveCollisionsFromSprite(_currentSpriteId);
            }
        }
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (_currentCompositionSampler != null
            && _currentCompositionSampler.RuntimeState.PartCount > 0
            && Animation2dBoundsCalculator.TryCalculateLocalBounds(_currentCompositionSampler.RuntimeState, _spriteDataById, out var composedBounds))
        {
            return composedBounds.Transform(WorldMatrixWithScale);
        }
        return GetDefaultBoundingBox();
    }

    private BoundingBox GetDefaultBoundingBox()
    {
        const float length = 0.5f;
        var min = Vector3.One * -length;
        var max = Vector3.One * length;
        return new BoundingBox(min, max).Transform(WorldMatrixWithScale);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        Color = element["color"].GetColor();
        SpriteEffect = element["sprite_effect"].GetEnum<SpriteEffects>();

        foreach (var animationNode in element["animations"])
        {
            _animationAssetIds.Add(animationNode.GetGuid());
        }
    }

    public List<Guid> AnimationAssetIds => _animationAssetIds;

}
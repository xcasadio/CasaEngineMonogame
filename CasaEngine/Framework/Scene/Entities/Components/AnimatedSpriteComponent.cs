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

    //Ghost bodies of the collision timeline, built once per (animation, collision keyframe) and pooled:
    //a keyframe change only removes the outgoing bodies and re-adds the incoming ones.
    private readonly Dictionary<Animation2dCompositionSampler, List<PhysicsBody>[]> _collisionBodiesBySampler = new();
    private readonly List<int> _fixtureGroupProfileIds = new();
    private readonly List<List<ColliderFixture>> _fixtureGroups = new();
    private Animation2dCompositionSampler _activeCollisionSampler;
    private List<PhysicsBody> _activeCollisionBodies;
    private int _activeCollisionKeyframeIndex = -1;

    private readonly List<Guid> _animationAssetIds = new();

    //Animations handed to AddAnimation instead of being named by an asset id. InitializeWithWorld
    //rebuilds Animations from scratch for the world the component enters, so it has to re-register
    //these too: nothing else remembers them, and dropping them left the component with no animation
    //at all - and so with a null CurrentAnimation - for every caller that builds its sprite in code.
    private readonly List<Animation2d> _animationsAddedByCode = new();
    private readonly Dictionary<Guid, Sprite> _spriteById = new();
    private readonly Dictionary<Guid, SpriteData> _spriteDataById = new();

    // ADR-0037: sprite data and animation data are shared assets, held through counted handles for as
    // long as this component references them, and given back in ReleaseSpriteHandles /
    // ReleaseAnimationDataHandles (InitializeWithWorld's reset and Detach). Each Sprite in _spriteById
    // holds its own sheet texture handle and is disposed alongside its sprite data handle.
    private readonly Dictionary<Guid, AssetHandle<SpriteData>> _spriteDataHandleById = new();
    private readonly List<AssetHandle<Animation2dData>> _animationDataHandles = new();

    private readonly List<Guid> _spriteIdsToResolve = new();
    private readonly Dictionary<Animation2d, Animation2dCompositionSampler> _compositionSamplerByAnimation = new();

    private AssetContentManager _assetContentManager;
    private IPhysicsWorld _physicsWorldContext;
    private SpriteRendererComponent _spriteRenderer;
    private DepthSortable2DComponent _depthSortable2DComponent;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d CurrentAnimation { get; private set; }
    public bool IsPlaybackPaused { get; set; }
    public float CurrentAnimationTimeSeconds => _currentCompositionSampler?.CurrentTime ?? 0f;
    private Animation2dCompositionSampler _currentCompositionSampler;
    private Guid _currentSpriteId;
    public Animation2dCompositionRuntimeState CurrentCompositionState => _currentCompositionSampler?.RuntimeState;
    public List<Animation2d> Animations { get; } = new();

    [Browsable(false)]
    public PhysicsType PhysicsType => PhysicsType.Kinetic;

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; } = new();

    /// <summary>
    /// Opt-in of the animation driven physics volumes: when false this component activates no collision
    /// fixture set of the timeline of its animations.
    /// </summary>
    public bool CreatePhysicsForEachFrame { get; set; } = true;

    public AnimatedSpriteComponent()
    {
        Color = Color.White;
        SpriteEffect = SpriteEffects.None;
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public AnimatedSpriteComponent(Guid id) : base(id)
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
        _animationsAddedByCode.AddRange(other._animationsAddedByCode);
    }

    public void SetCurrentAnimation(Animation2d anim, bool forceReset)
    {
        if (CurrentAnimation != null && CurrentAnimation.Animation2dData.Name == anim.Animation2dData.Name)
        {
            if (forceReset)
            {
                CurrentAnimation.Reset();
                _currentCompositionSampler?.Reset();
                UpdateCurrentSprite();
                UpdateCollisionTimeline();
            }

            return;
        }

        CurrentAnimation = anim;
        CurrentAnimation.Reset();
        _compositionSamplerByAnimation.TryGetValue(CurrentAnimation, out _currentCompositionSampler);
        _currentCompositionSampler?.Reset();

        _currentSpriteId = Guid.Empty;
        UpdateCurrentSprite();
        UpdateCollisionTimeline();
    }

    public void SetCurrentAnimation(int index, bool forceReset)
    {
        SetCurrentAnimation(Animations[index], forceReset);
    }

    public bool SetCurrentAnimation(string name, bool forceReset)
    {
        foreach (var anim in Animations)
        {
            if (anim.Animation2dData.Name == name)
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

        //The pooled bodies belong to the world this component leaves, so they go before the new context.
        DestroyCollisionBodies();
        _physicsWorldContext = Owner.World.PhysicsWorld;

        Animations.Clear();
        _compositionSamplerByAnimation.Clear();
        _currentCompositionSampler = null;
        //CurrentAnimation is one of the animations just dropped: keeping it would make the
        //SetCurrentAnimation below take its same-name shortcut and leave the sampler unbound.
        CurrentAnimation = null;
        _currentSpriteId = Guid.Empty;
        ReleaseSpriteHandles();
        ReleaseAnimationDataHandles();

        foreach (var assetId in _animationAssetIds)
        {
            var animationDataHandle = Owner.World.Game.AssetContentManager.Acquire<Animation2dData>(assetId);
            _animationDataHandles.Add(animationDataHandle);
            RegisterAnimation(new Animation2d(animationDataHandle.Asset));
        }

        foreach (var animation2d in _animationsAddedByCode)
        {
            RegisterAnimation(animation2d);
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

        if (CurrentAnimation != null && !IsPlaybackPaused)
        {
            var wasFinished = _currentCompositionSampler?.IsFinished == true;
            var isFinished = _currentCompositionSampler?.Update(elapsedTime) == true;
            IsBoundingBoxDirty = true;
            UpdateCurrentSprite();
            UpdateCollisionTimeline();

            if (!wasFinished && isFinished)
            {
                AnimationFinished?.Invoke(this, CurrentAnimation);
            }
        }

        base.Update(elapsedTime);
    }

    public bool SeekCurrentAnimation(float timeSeconds)
    {
        if (_currentCompositionSampler == null)
        {
            return false;
        }

        _currentCompositionSampler.Seek(timeSeconds);
        IsBoundingBoxDirty = true;
        UpdateCurrentSprite();
        UpdateCollisionTimeline();
        return true;
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
        _animationsAddedByCode.Add(animation2d);
        RegisterAnimation(animation2d);
    }

    /// <summary>
    /// Puts one animation in <see cref="Animations"/> with the composition sampler and the sprites it
    /// needs. Shared by <see cref="AddAnimation"/> and by the rebuild of
    /// <see cref="InitializeWithWorld"/> so that an animation is registered the same way whether it
    /// came from an asset id or from code.
    /// </summary>
    private void RegisterAnimation(Animation2d animation2d)
    {
        animation2d.Initialize();
        Animations.Add(animation2d);
        RegisterCompositionSampler(animation2d);
        if (_assetContentManager != null)
        {
            CacheAnimationSprites(animation2d.Animation2dData);
        }
    }

    public bool ReloadSpriteAsset(Guid spriteAssetId, SpriteData spriteData)
    {
        ArgumentNullException.ThrowIfNull(spriteData);

        if (spriteAssetId == Guid.Empty
            || _assetContentManager == null
            || (!_spriteById.ContainsKey(spriteAssetId) && !_spriteDataById.ContainsKey(spriteAssetId)))
        {
            return false;
        }

        bool wasCurrentSprite = _currentSpriteId == spriteAssetId;

        // spriteData is supplied directly by the reload service, not resolved through Acquire here: give
        // back whatever sprite data hold this component took itself, so it does not keep pinning a stale
        // instance the manager may have already replaced.
        if (_spriteDataHandleById.Remove(spriteAssetId, out var staleDataHandle))
        {
            staleDataHandle.Dispose();
        }
        _spriteDataById[spriteAssetId] = spriteData;

        if (_spriteById.Remove(spriteAssetId, out var staleSprite))
        {
            staleSprite.Dispose();
        }
        _spriteById[spriteAssetId] = Sprite.Create(spriteData, _assetContentManager);

        IsBoundingBoxDirty = true;
        if (wasCurrentSprite)
        {
            UpdateCurrentSprite();
        }

        return true;
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

        // All parts of a composed animation share the entity's own sort coordinate (built once
        // here, from the component's world position, not from any per-part position); DrawOrder
        // (via BuildPartSortKey's LocalSortOffset) is what arbitrates draw order within the entity.
        var baseSortKey = BuildComposedAnimationBaseSortKey();

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
                var sortKey = BuildPartSortKey(baseSortKey, part);
                _spriteRenderer.DrawSprite(sprite, partPosition, part.Rotation, scale, Color, Position.Z, sortKey, spriteEffects);
                continue;
            }

            var zOrder = Position.Z - part.DrawOrder * 0.0001f - part.SourceIndex * 0.000001f;
            _spriteRenderer.DrawSprite(sprite, partPosition, part.Rotation, scale, Color, zOrder, spriteEffects);
        }
    }

    /// <summary>
    /// The single sort key every part of the current composed animation is derived from, built once
    /// per draw call from the component's own world <see cref="Position"/> (never from a per-part
    /// position). Internal (rather than private) so <c>CasaEngine.Tests</c> can exercise this exact
    /// production computation instead of re-implementing it.
    /// </summary>
    internal RenderSortKey2D BuildComposedAnimationBaseSortKey()
    {
        return _depthSortable2DComponent != null
            ? _depthSortable2DComponent.BuildSortKey(Position, Owner.World.CurrentRenderFrame)
            : default;
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

    /// <summary>
    /// Derives one part's sort key from the shared <see cref="BuildComposedAnimationBaseSortKey"/> key:
    /// same <see cref="RenderSortKey2D.SortCoordinate"/> for every part of the entity, <c>DrawOrder</c>
    /// (via <see cref="RenderSortKey2D.LocalSortOffset"/>) and <c>SourceIndex</c> arbitrate within it.
    /// Internal so <c>CasaEngine.Tests</c> can call it directly instead of reflecting into it.
    /// </summary>
    internal static RenderSortKey2D BuildPartSortKey(RenderSortKey2D baseSortKey, Animation2dPartRuntimeState part)
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

        AssetHandle<SpriteData> spriteDataHandle;
        try
        {
            spriteDataHandle = _assetContentManager.Acquire<SpriteData>(spriteId);
        }
        catch (InvalidOperationException exception)
        {
            Logs.WriteError($"AnimatedSpriteComponent : can't resolve sprite '{spriteId}' ({exception.Message})");
            return null;
        }

        var spriteData = spriteDataHandle.Asset;
        sprite = Sprite.Create(spriteData, _assetContentManager);
        _spriteById.Add(spriteId, sprite);
        _spriteDataById[spriteId] = spriteData;
        _spriteDataHandleById[spriteId] = spriteDataHandle;
        return sprite;
    }

    /// <summary>
    /// Tracks the sprite drawn first by the current animation. It drives the bounding box only:
    /// collision volumes come from the collision timeline of the animation, never from the sprite.
    /// </summary>
    private void UpdateCurrentSprite()
    {
        var spriteId = Guid.Empty;
        var runtimeState = _currentCompositionSampler?.RuntimeState;

        if (runtimeState != null)
        {
            for (var drawIndex = 0; drawIndex < runtimeState.DrawPartIndices.Count; drawIndex++)
            {
                var part = runtimeState.Parts[runtimeState.DrawPartIndices[drawIndex]];
                if (part.Visible && part.SpriteId != Guid.Empty)
                {
                    spriteId = part.SpriteId;
                    break;
                }
            }
        }

        if (_currentSpriteId == spriteId)
        {
            return;
        }

        _currentSpriteId = spriteId;
        IsBoundingBoxDirty = true;
    }

    /// <summary>
    /// Activates the collision fixture set of the current animation time. The bodies of a set are built
    /// once and pooled: a keyframe change removes the outgoing bodies from the world and re-adds the
    /// incoming ones, so the steady state allocates nothing.
    /// </summary>
    private void UpdateCollisionTimeline()
    {
        if (_physicsWorldContext == null)
        {
            return;
        }

        //A disabled owner never owns active collision bodies (same convention as ParticleSystemComponent
        //and StaticSpriteComponent): Entity.IsEnabled does not gate Entity.Update, so this method still
        //runs every frame on a disabled owner and must not resurrect the bodies of the last keyframe.
        var sampler = CreatePhysicsForEachFrame && Owner is { IsEnabled: true } ? _currentCompositionSampler : null;
        int keyframeIndex = sampler?.CurrentCollisionKeyframeIndex ?? -1;

        if (ReferenceEquals(sampler, _activeCollisionSampler) && keyframeIndex == _activeCollisionKeyframeIndex)
        {
            UpdateActiveCollisionBodyTransforms(refreshAabb: true);
            return;
        }

        RemoveActiveCollisionBodies();

        _activeCollisionSampler = sampler;
        _activeCollisionKeyframeIndex = keyframeIndex;
        _activeCollisionBodies = sampler != null && keyframeIndex >= 0
            ? GetOrCreateCollisionBodies(sampler, keyframeIndex)
            : null;

        UpdateActiveCollisionBodyTransforms(refreshAabb: false);
        AddActiveCollisionBodies();
    }

    /// <summary>
    /// Places the active bodies on the logical pose of the entity root. The volumes of a fixture set live
    /// in the space the world simulates, never in the space this component renders in, so an animated
    /// sprite may sit under a <see cref="RenderProjectionComponent"/> without displacing its volumes.
    /// </summary>
    private void UpdateActiveCollisionBodyTransforms(bool refreshAabb)
    {
        if (_activeCollisionBodies == null || _activeCollisionBodies.Count == 0)
        {
            return;
        }

        var worldMatrix = GetLogicalWorldMatrix();

        for (int i = 0; i < _activeCollisionBodies.Count; i++)
        {
            var body = _activeCollisionBodies[i];
            body.WorldTransform = worldMatrix;

            if (refreshAabb)
            {
                _physicsWorldContext.RefreshBodyAabb(body);
            }
        }
    }

    private void AddActiveCollisionBodies()
    {
        if (_activeCollisionBodies == null)
        {
            return;
        }

        for (int i = 0; i < _activeCollisionBodies.Count; i++)
        {
            _physicsWorldContext.AddCollisionObject(_activeCollisionBodies[i]);
        }
    }

    private void RemoveActiveCollisionBodies()
    {
        if (_activeCollisionBodies == null)
        {
            return;
        }

        for (int i = 0; i < _activeCollisionBodies.Count; i++)
        {
            _physicsWorldContext.RemoveCollisionObject(_activeCollisionBodies[i]);
        }

        _physicsWorldContext.ClearCollisionDataFrom(this);
        _activeCollisionBodies = null;
    }

    private List<PhysicsBody> GetOrCreateCollisionBodies(Animation2dCompositionSampler sampler, int keyframeIndex)
    {
        if (!_collisionBodiesBySampler.TryGetValue(sampler, out var bodiesByKeyframe))
        {
            bodiesByKeyframe = new List<PhysicsBody>[sampler.CollisionKeyframes.Count];
            _collisionBodiesBySampler.Add(sampler, bodiesByKeyframe);
        }

        var bodies = bodiesByKeyframe[keyframeIndex];
        if (bodies == null)
        {
            bodies = CreateCollisionBodies(sampler.CollisionKeyframes[keyframeIndex]);
            bodiesByKeyframe[keyframeIndex] = bodies;
        }

        return bodies;
    }

    private List<PhysicsBody> CreateCollisionBodies(Animation2dCollisionKeyframeData collisionKeyframe)
    {
        BuildFixtureGroups(collisionKeyframe.Fixtures);

        var collisionProfiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;
        var worldMatrix = GetLogicalWorldMatrix();
        var localScale = GetLogicalLocalScale();
        var bodies = new List<PhysicsBody>(_fixtureGroupProfileIds.Count);

        for (int i = 0; i < _fixtureGroupProfileIds.Count; i++)
        {
            int profileId = _fixtureGroupProfileIds[i];
            bodies.Add(_physicsWorldContext.CreateGhostObject(worldMatrix, this, _fixtureGroups[i], localScale,
                profileId, collisionProfiles.GetProfile(profileId).DebugColor));
        }

        return bodies;
    }

    /// <summary>
    /// Splits the fixtures of a keyframe per resolved collision profile, keeping the fixture order inside
    /// each group. A fixture naming no profile is a trigger, like an authored sprite volume.
    /// </summary>
    private void BuildFixtureGroups(List<ColliderFixture> fixtures)
    {
        for (int i = 0; i < _fixtureGroups.Count; i++)
        {
            _fixtureGroups[i].Clear();
        }

        _fixtureGroupProfileIds.Clear();

        var collisionProfiles = GameSettings.PhysicsEngineSettings.CollisionProfiles;

        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            if (fixture.Shape == null)
            {
                throw new InvalidOperationException(
                    $"Collider fixture {i} of a collision keyframe of '{CurrentAnimation?.Animation2dData?.Name}' has no shape.");
            }

            int profileId = string.IsNullOrEmpty(fixture.ProfileName)
                ? CollisionProfileIds.Trigger
                : collisionProfiles.GetProfileId(fixture.ProfileName);

            int groupIndex = _fixtureGroupProfileIds.IndexOf(profileId);
            if (groupIndex < 0)
            {
                groupIndex = _fixtureGroupProfileIds.Count;
                _fixtureGroupProfileIds.Add(profileId);

                if (_fixtureGroups.Count <= groupIndex)
                {
                    _fixtureGroups.Add(new List<ColliderFixture>());
                }
            }

            _fixtureGroups[groupIndex].Add(fixture);
        }
    }

    private void DestroyCollisionBodies()
    {
        if (_physicsWorldContext != null)
        {
            foreach (var bodiesByKeyframe in _collisionBodiesBySampler.Values)
            {
                for (int i = 0; i < bodiesByKeyframe.Length; i++)
                {
                    var bodies = bodiesByKeyframe[i];
                    if (bodies == null)
                    {
                        continue;
                    }

                    for (int bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
                    {
                        _physicsWorldContext.RemoveCollisionObject(bodies[bodyIndex]);
                        bodies[bodyIndex].Dispose();
                    }
                }
            }

            _physicsWorldContext.ClearCollisionDataFrom(this);
        }

        _collisionBodiesBySampler.Clear();
        _activeCollisionBodies = null;
        _activeCollisionSampler = null;
        _activeCollisionKeyframeIndex = -1;
    }

    /// <summary>Logical pose of the entity: the pose the simulation uses, taken from the entity root.</summary>
    private Matrix GetLogicalWorldMatrix()
    {
        var root = Owner?.RootComponent;
        return root != null ? root.WorldMatrixNoScale : WorldMatrixNoScale;
    }

    private Vector3 GetLogicalLocalScale()
    {
        var root = Owner?.RootComponent;
        return root != null ? root.LocalScale : LocalScale;
    }

    private void OnAnimationEventTriggered(AnimationEventAsset animationEvent)
    {
        AnimationEventTriggered?.Invoke(this, animationEvent);
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (Owner.IsEnabled)
        {
            UpdateCollisionTimeline();
        }
        else
        {
            DestroyCollisionBodies();
        }
    }

    public override void Detach()
    {
        DestroyCollisionBodies();
        ReleaseSpriteHandles();
        ReleaseAnimationDataHandles();
        base.Detach();
    }

    /// <summary>Disposes every held sprite (and its sprite data hold), and clears the lookup tables.</summary>
    private void ReleaseSpriteHandles()
    {
        foreach (var sprite in _spriteById.Values)
        {
            sprite.Dispose();
        }
        _spriteById.Clear();

        foreach (var handle in _spriteDataHandleById.Values)
        {
            handle.Dispose();
        }
        _spriteDataHandleById.Clear();
        _spriteDataById.Clear();
    }

    /// <summary>Gives back every animation data hold taken in <see cref="InitializeWithWorld"/>.</summary>
    private void ReleaseAnimationDataHandles()
    {
        for (var index = 0; index < _animationDataHandles.Count; index++)
        {
            _animationDataHandles[index].Dispose();
        }
        _animationDataHandles.Clear();
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
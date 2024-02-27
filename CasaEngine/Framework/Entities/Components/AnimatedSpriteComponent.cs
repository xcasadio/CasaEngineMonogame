using System.ComponentModel;
using BulletSharp;
using CasaEngine.Core.Design;
using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : SceneComponent, ICollideableComponent, IComponentDrawable, IBoundingBoxable
{
    public event EventHandler<Guid>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;

    private readonly Dictionary<Guid, List<(Shape2d, CollisionObject)>> _collisionObjectByFrameId = new();
    private readonly List<Guid> _animationAssetIds = new();

    private AssetContentManager _assetContentManager;
    private PhysicsEngineComponent? _physicsEngineComponent;
    private SpriteRendererComponent _spriteRenderer;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d? CurrentAnimation { get; private set; }
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
            }

            return;
        }

        if (CurrentAnimation != null)
        {
            RemoveCollisionsFromFrame(CurrentAnimation.CurrentFrame);
            CurrentAnimation.FrameChanged -= OnFrameChanged;
            CurrentAnimation.AnimationFinished -= OnAnimationFinished;
        }

        CurrentAnimation = anim;
        CurrentAnimation.Reset();

        CurrentAnimation.FrameChanged += OnFrameChanged;
        CurrentAnimation.AnimationFinished += OnAnimationFinished;
        CurrentAnimation.FrameChanged += OnFrameChanged;
        AddOrUdpateCollisionFromFrame(CurrentAnimation.CurrentFrame, true);
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

    public Guid GetCurrentFrameName()
    {
        if (CurrentAnimation == null)
        {
            return Guid.Empty;
        }

        return CurrentAnimation.CurrentFrame;
    }

    public int GetCurrentFrameIndex()
    {
        if (CurrentAnimation == null)
        {
            return -1;
        }

        int i = 0;
        foreach (var frame in CurrentAnimation.Animation2dData.Frames)
        {
            if (frame.SpriteId == CurrentAnimation.CurrentFrame)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _spriteRenderer = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
        _assetContentManager = Owner.World.Game.AssetContentManager;
        _physicsEngineComponent = Owner.World.Game.GetGameComponent<PhysicsEngineComponent>();

        Animations.Clear();

        foreach (var assetId in _animationAssetIds)
        {
            var animation2dData = Owner.World.Game.AssetContentManager.Load<Animation2dData>(assetId);
            var animation2d = new Animation2d(animation2dData);
            animation2d.Initialize();
            Animations.Add(animation2d);
        }

        foreach (var animation in Animations)
        {
            foreach (var frame in animation.Animation2dData.Frames)
            {
                var spriteData = Owner.World.Game.AssetContentManager.Load<SpriteData>(frame.SpriteId);
                if (spriteData.CollisionShapes.Count == 0)
                {
                    continue;
                }

                if (!_collisionObjectByFrameId.ContainsKey(frame.SpriteId))
                {
                    _collisionObjectByFrameId.Add(frame.SpriteId, new List<(Shape2d, CollisionObject)>(1));
                }

                foreach (var collisionShape in spriteData.CollisionShapes)
                {
                    var color = collisionShape.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;
                    var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, LocalScale, WorldMatrixNoScale, _physicsEngineComponent, this, color);
                    if (collisionObject != null)
                    {
                        _collisionObjectByFrameId[frame.SpriteId].Add(new(collisionShape.Shape, collisionObject));
                    }
                }
            }
        }

#if EDITOR
        if (Animations.Count > 0)
        {
            SetCurrentAnimation(0, true);
        }
#endif
    }

    public override AnimatedSpriteComponent Clone()
    {
        return new AnimatedSpriteComponent(this);
    }

    public override void Update(float elapsedTime)
    {
#if EDITOR
        if (Owner.World.Game.IsRunningInGameEditorMode)
        {
            return;
        }
#endif

        if (CurrentAnimation != null)
        {
            CurrentAnimation?.Update(elapsedTime);
            UpdateCollisionFromFrame(CurrentAnimation.CurrentFrame);
        }

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (CurrentAnimation != null)
        {
            var spriteData = _assetContentManager.GetAsset<SpriteData>(CurrentAnimation.CurrentFrame);

            if (spriteData == null)
            {
                Logs.WriteError($"AnimatedSpriteComponent : the sprite of the current frame doesn't exist '{CurrentAnimation.CurrentFrame}'");
                return;
            }

            //TODO : create list with all spriteData
            var sprite = Sprite.Create(spriteData, _assetContentManager);
            _spriteRenderer.DrawSprite(sprite,
                new Vector2(Position.X, Position.Y),
                0.0f,
                new Vector2(Scale.X, Scale.Y),
                Color.White,
                Position.Z);
        }
    }

    public void AddAnimation(Animation2d animation2d)
    {
        animation2d.Initialize();
        Animations.Add(animation2d);
    }

    private SpriteData? GetCurrentSpriteData()
    {
        foreach (var frame in CurrentAnimation.Animation2dData.Frames)
        {
            if (frame.SpriteId == CurrentAnimation.CurrentFrame)
            {
                return Owner.World.Game.AssetContentManager.GetAsset<SpriteData>(frame.SpriteId);
            }
        }
        return null;
    }

    private void OnFrameChanged(object? sender, (Guid oldFrame, Guid newFrame) arg)
    {
        IsBoundingBoxDirty = true;
        RemoveCollisionsFromFrame(arg.oldFrame);
        AddOrUdpateCollisionFromFrame(arg.newFrame, true);

        FrameChanged?.Invoke(this, arg.newFrame);
    }

    private void UpdateCollisionFromFrame(Guid frameId)
    {
        AddOrUdpateCollisionFromFrame(frameId, false);
    }

    private void AddOrUdpateCollisionFromFrame(Guid frameId, bool addCollision)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var collisionObjects)
            && CreatePhysicsForEachFrame)
        {
            var spriteData = _assetContentManager.GetAsset<SpriteData>(frameId);

            foreach (var (shape2d, collisionObject) in collisionObjects)
            {
                Physics2dHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                    collisionObject, shape2d, spriteData.Origin, spriteData.PositionInTexture);
                if (addCollision)
                {
                    _physicsEngineComponent.AddCollisionObject(collisionObject);
                }
            }
        }
    }

    public void RemoveCollisionsFromFrame(Guid frameId)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var collisionObjects))
        {
            foreach (var (shape2d, collisionObject) in collisionObjects)
            {
                _physicsEngineComponent.RemoveCollisionObject(collisionObject);
                _physicsEngineComponent.ClearCollisionDataFrom(this);
            }
        }
    }

    private void OnAnimationFinished(object? sender, EventArgs args)
    {
        AnimationFinished?.Invoke(this, (Animation2d)sender);
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (CurrentAnimation != null)
        {
            if (Owner.IsEnabled)
            {
                AddOrUdpateCollisionFromFrame(CurrentAnimation.CurrentFrame, true);
            }
            else
            {
                RemoveCollisionsFromFrame(CurrentAnimation.CurrentFrame);
            }
        }
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (CurrentAnimation != null)
        {
            var spriteData = _assetContentManager.GetAsset<SpriteData>(CurrentAnimation.CurrentFrame);
            var halfWidth = spriteData.PositionInTexture.Width / 2f;
            var halfHeight = spriteData.PositionInTexture.Height / 2f;

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

#if EDITOR
    public List<Guid> AnimationAssetIds => _animationAssetIds;

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        var newJObject = new JObject();
        Color.Save(newJObject);
        jObject.Add("color", newJObject);

        jObject.Add("sprite_effect", SpriteEffect.ConvertToString());

        var animationsNode = new JArray();

        foreach (var animationId in AnimationAssetIds)
        {
            animationsNode.Add(animationId);
        }

        jObject.Add("animations", animationsNode);
    }

#endif
}
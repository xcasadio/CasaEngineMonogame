using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using BulletSharp;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : Component, ICollideableComponent
{
    public override int ComponentId => (int)ComponentIds.AnimatedSprite;

    public event EventHandler<long>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;

    private readonly Dictionary<long, List<(Shape2d, CollisionObject)>> _collisionObjectByFrameId = new();
    private readonly List<long> _animationAssetIds = new();

    private CasaEngineGame _game;
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

    public AnimatedSpriteComponent() : base()
    {
        Color = Color.White;
        SpriteEffect = SpriteEffects.None;
    }

    public void SetCurrentAnimation(Animation2d anim, bool forceReset)
    {
        if (CurrentAnimation != null && CurrentAnimation.AnimationData.AssetInfo.Name == anim.AnimationData.AssetInfo.Name)
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
            if (anim.AnimationData.AssetInfo.Name == name)
            {
                SetCurrentAnimation(anim, forceReset);
                return true;
            }
        }

        return false;
    }

    public long GetCurrentFrameName()
    {
        if (CurrentAnimation == null)
        {
            return -1;
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

    public override void Initialize(Entity entity, CasaEngineGame game)
    {
        base.Initialize(entity, game);

        _game = game;
        _spriteRenderer = game.GetGameComponent<SpriteRendererComponent>();
        _assetContentManager = game.GameManager.AssetContentManager;
        _physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();

        foreach (var assetId in _animationAssetIds)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(assetId);
            var animation2dData = game.GameManager.AssetContentManager.Load<Animation2dData>(assetInfo);
            var animation2d = new Animation2d(animation2dData);
            animation2d.Initialize();
            Animations.Add(animation2d);
        }

        foreach (var animation in Animations)
        {
            foreach (var frame in animation.Animation2dData.Frames)
            {
                var assetInfo = GameSettings.AssetInfoManager.Get(frame.SpriteId);
                var spriteData = game.GameManager.AssetContentManager.Load<SpriteData>(assetInfo);
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
                    var collisionObject = Physics2dHelper.CreateCollisionsFromSprite(collisionShape, Owner, _physicsEngineComponent, this, color);
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

    public override void Update(float elapsedTime)
    {
        if (CurrentAnimation != null)
        {
            CurrentAnimation?.Update(elapsedTime);
            UpdateCollisionFromFrame(CurrentAnimation.CurrentFrame);
        }
    }

    public override void Draw()
    {
        if (CurrentAnimation != null)
        {
            var spriteData = _assetContentManager.GetAsset<SpriteData>(CurrentAnimation.CurrentFrame);

            if (spriteData == null)
            {
                LogManager.Instance.WriteLineError($"AnimatedSpriteComponent : the sprite of the current frame doesn't exist '{CurrentAnimation.CurrentFrame}'");
                return;
            }

            var sprite = Sprite.Create(spriteData, _assetContentManager);
            var worldMatrix = Owner.Coordinates.WorldMatrix;
            _spriteRenderer.DrawSprite(sprite,
                new Vector2(worldMatrix.Translation.X, worldMatrix.Translation.Y),
                0.0f,
                new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y),
                Color.White,
                Owner.Coordinates.Position.Z);
        }
    }

    /*void HandleEvent(const Event* pEvent_)
    {

    }*/

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
                return _game.GameManager.AssetContentManager.GetAsset<SpriteData>(frame.SpriteId);
            }
        }
        return null;
    }

    private void OnFrameChanged(object? sender, (long oldFrame, long newFrame) arg)
    {
        RemoveCollisionsFromFrame(arg.oldFrame);
        AddOrUdpateCollisionFromFrame(arg.newFrame, true);

        FrameChanged?.Invoke(this, arg.newFrame);
    }

    private void UpdateCollisionFromFrame(long frameId)
    {
        AddOrUdpateCollisionFromFrame(frameId, false);
    }

    private void AddOrUdpateCollisionFromFrame(long frameId, bool addCollision)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var collisionObjects))
        {
            var spriteData = _assetContentManager.GetAsset<SpriteData>(frameId);

            foreach (var (shape2d, collisionObject) in collisionObjects)
            {
                Physics2dHelper.UpdateBodyTransformation(Owner, collisionObject, shape2d, spriteData.Origin, spriteData.PositionInTexture);
                if (addCollision)
                {
                    _physicsEngineComponent.AddCollisionObject(collisionObject);
                }
            }
        }
    }

    public void RemoveCollisionsFromFrame(long frameId)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var collisionObjects))
        {
            foreach (var (shape2d, collisionObject) in collisionObjects)
            {
                _physicsEngineComponent.RemoveCollisionObject(collisionObject);
                _physicsEngineComponent.ClearCollisionDataOf(this);
            }
        }
    }

    private void OnAnimationFinished(object? sender, EventArgs args)
    {
        AnimationFinished?.Invoke(this, (Animation2d)sender);
    }

    public override void OnEnabledValueChange()
    {
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

    public void OnHit(Collision collision)
    {
        Owner.Hit(collision, this);
    }

    public void OnHitEnded(Collision collision)
    {
        Owner.HitEnded(collision, this);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        Color = element.GetProperty("color").GetColor();
        SpriteEffect = element.GetProperty("sprite_effect").GetEnum<SpriteEffects>();

        foreach (var animationNode in element.GetProperty("animations").EnumerateArray())
        {
            _animationAssetIds.Add(animationNode.GetInt32());
        }
    }

    public override Component Clone()
    {
        var component = new AnimatedSpriteComponent();

        component.Color = Color;
        component.SpriteEffect = SpriteEffect;
        component.CurrentAnimation = CurrentAnimation;
        component.Animations.AddRange(Animations);
        component._animationAssetIds.AddRange(_animationAssetIds);

        return component;
    }

#if EDITOR
    public List<long> AnimationAssetIds => _animationAssetIds;

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var newJObject = new JObject();
        Color.Save(newJObject);
        jObject.Add("color", newJObject);

        jObject.Add("sprite_effect", SpriteEffect.ConvertToString());

        var animationsNode = new JArray();

        foreach (var animation2d in Animations)
        {
            animationsNode.Add(animation2d.Animation2dData.AssetInfo.Id);
        }

        jObject.Add("animations", animationsNode);
    }

#endif
}
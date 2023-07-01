using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using BulletSharp;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Renderer;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : Component, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.AnimatedSprite;

    public event EventHandler<string>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;
    private readonly Dictionary<string, List<(Shape2d, CollisionObject)>> _collisionObjectByFrameId = new();

    private Renderer2dComponent _renderer2dComponent;
    private AssetContentManager _assetContentManager;
    private PhysicsEngineComponent? _physicsEngineComponent;
    private CasaEngineGame _game;
    private SpriteRendererComponent _spriteRenderer;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d? CurrentAnimation { get; private set; }
    public List<Animation2d> Animations { get; } = new();

    public PhysicsType PhysicsType => PhysicsType.Kinetic;
    public HashSet<Collision> Collisions { get; } = new();

    public AnimatedSpriteComponent(Entity entity) : base(entity, ComponentId)
    {
        Color = Color.White;
        SpriteEffect = SpriteEffects.None;
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
        AddCollisionFromFrame(CurrentAnimation.CurrentFrame, true);
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

    public string GetCurrentFrameName()
    {
        if (CurrentAnimation == null)
        {
            return "";
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

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _renderer2dComponent = game.GetGameComponent<Renderer2dComponent>();
        _spriteRenderer = game.GetGameComponent<SpriteRendererComponent>();
        _assetContentManager = game.GameManager.AssetContentManager;
        _physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();

        foreach (var animation in Animations)
        {
            foreach (var frame in animation.Animation2dData.Frames)
            {
                var spriteData = game.GameManager.AssetContentManager.GetAsset<SpriteData>(frame.SpriteId);
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

    private void OnFrameChanged(object? sender, (string oldFrame, string newFrame) arg)
    {
        RemoveCollisionsFromFrame(arg.oldFrame);
        AddCollisionFromFrame(arg.newFrame, true);

        FrameChanged?.Invoke(this, arg.newFrame);
    }

    private void UpdateCollisionFromFrame(string frameId)
    {
        AddCollisionFromFrame(frameId, false);
    }

    private void AddCollisionFromFrame(string frameId, bool addCollision)
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

    public void RemoveCollisionsFromFrame(string frameId)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var collisionObjects))
        {
            foreach (var (shape2d, collisionObject) in collisionObjects)
            {
                _physicsEngineComponent.RemoveCollisionObject(collisionObject);
            }
        }
    }

    private void OnAnimationFinished(object? sender, EventArgs args)
    {
        AnimationFinished?.Invoke(this, (Animation2d)sender);
    }

    public void OnHit(Collision collision)
    {
        //Owner.OnHit(collision)
    }

    public void OnHitEnded(Collision collision)
    {

    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        throw new NotImplementedException();
        base.Save(jObject);
    }

#endif
}
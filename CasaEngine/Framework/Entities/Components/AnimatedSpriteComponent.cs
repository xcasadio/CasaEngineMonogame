using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CasaEngine.Core.Logger;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using System.Transactions;
using BulletSharp.SoftBody;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.AnimatedSprite;

    public event EventHandler<string>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;

    //Dictionary<string, List<ICollisionObjectContainer>> _collisionObjectByFrameId;
    private Renderer2dComponent _renderer2dComponent;
    private AssetContentManager _assetContentManager;
    private Dictionary<string, Body> _collisionObjectByFrameId = new();
    private PhysicsEngineComponent? _physicsEngineComponent;
    private CasaEngineGame _game;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d? CurrentAnimation { get; private set; }
    public List<Animation2d> Animations { get; } = new();

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
            CurrentAnimation.FrameChanged -= OnFrameChanged;
            CurrentAnimation.AnimationFinished -= OnAnimationFinished;
        }

        CurrentAnimation = anim;
        CurrentAnimation.Reset();

        CurrentAnimation.FrameChanged += OnFrameChanged;
        CurrentAnimation.AnimationFinished += OnAnimationFinished;
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
        _assetContentManager = game.GameManager.AssetContentManager;
        _physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();

        foreach (var animation in Animations)
        {
            foreach (var frame in animation.Animation2dData.Frames)
            {
                if (_collisionObjectByFrameId.ContainsKey(frame.SpriteId))
                {
                    continue;
                }

                var spriteData = game.GameManager.AssetContentManager.GetAsset<SpriteData>(frame.SpriteId);
                //var body = Physics2dHelper.CreateCollisionsFromSprite(spriteData, Owner, _physicsEngine2dProxyComponent.Physic2dWorld);
                //if (body != null)
                //{
                //    body.OnCollision = OnCollision;
                //    body.OnSeparation = OnSeparation;
                //    _collisionObjectByFrameId[frame.SpriteId] = body;
                //}
            }
        }
    }

    public override void Update(float elapsedTime)
    {
        if (CurrentAnimation != null)
        {
            CurrentAnimation.Update(elapsedTime);

            if (_collisionObjectByFrameId.TryGetValue(CurrentAnimation.CurrentFrame, out var body))
            {
                //UpdateBodyTransformation(body);
            }
        }
    }

    //private void UpdateBodyTransformation(Body body)
    //{
    //    var spriteData = GetCurrentSpriteData();
    //
    //
    //
    //    ShapeRectangle rect = spriteData.CollisionShapes[0].Shape as ShapeRectangle;
    //    //body.FixtureList[0].Shape //scale : change the shape
    //    //body.Position = new Vector2(Owner.Coordinates.Position.X, Owner.Coordinates.Position.Y);
    //    body.Position = new Vector2(Owner.Coordinates.Position.X - spriteData.Origin.X + rect.Location.X + rect.Width / 2,
    //        Owner.Coordinates.Position.Y - spriteData.Origin.Y + rect.Location.Y + rect.Height / 2);
    //}

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
            _renderer2dComponent.DrawSprite(sprite, //TODO : load all sprites in a dictionary<name, sprite>
                spriteData,
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

    public void RemoveCollisionsFromLastFrame(string frameId)
    {
        if (_collisionObjectByFrameId.TryGetValue(frameId, out var body))
        {
            //body.Enabled = false;
        }
    }

    private void OnFrameChanged(object? sender, (string oldFrame, string newFrame) arg)
    {
        RemoveCollisionsFromLastFrame(arg.oldFrame);
        if (_collisionObjectByFrameId.TryGetValue(arg.newFrame, out var body))
        {
            //UpdateBodyTransformation(body);
            //body.Enabled = true;
        }

        FrameChanged?.Invoke(this, arg.newFrame);
    }

    private void OnAnimationFinished(object? sender, EventArgs args)
    {
        AnimationFinished?.Invoke(this, (Animation2d)sender);
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

public class Physics2dHelper
{
    //public static Body? CreateCollisionsFromSprite(SpriteData spriteData, Entity entity, Genbox.VelcroPhysics.Dynamics.World world)
    //{
    //    Body? body = null;
    //
    //    foreach (var collisionShape in spriteData.CollisionShapes)
    //    {
    //        switch (collisionShape.Shape.Type)
    //        {
    //            case Shape2dType.Compound:
    //                break;
    //            case Shape2dType.Polygone:
    //                break;
    //            case Shape2dType.Rectangle:
    //                var rectangle = collisionShape.Shape as ShapeRectangle;
    //                body = BodyFactory.CreateRectangle(world, rectangle.Width, rectangle.Height, 1,
    //                    new Vector2(rectangle.Location.X, rectangle.Location.Y),
    //                    rectangle.Rotation, BodyType.Kinematic, entity);
    //                break;
    //            case Shape2dType.Circle:
    //                break;
    //            case Shape2dType.Line:
    //                break;
    //            default:
    //                throw new ArgumentOutOfRangeException();
    //        }
    //    }
    //
    //    if (body != null)
    //    {
    //        body.Enabled = false;
    //    }
    //
    //    return body;
    //}
}
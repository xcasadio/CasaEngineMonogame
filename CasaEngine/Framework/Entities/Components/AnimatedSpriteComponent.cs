using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.AnimatedSprite;

    public event EventHandler<string>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;

    //Dictionary<string, List<ICollisionObjectContainer>> _collisionObjectByFrameId;
    private string _lastFrameId;
    private Renderer2dComponent _renderer2dComponent;
    private AssetContentManager _assetContentManager;

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
        base.Initialize(game);

        _renderer2dComponent = game.GetGameComponent<Renderer2dComponent>();
        _assetContentManager = game.GameManager.AssetContentManager;

        foreach (var animation in Animations)
        {
            foreach (var frame in animation.Animation2dData.Frames)
            {
                //if (_collisionObjectByFrameId.find(frame._spriteId) != _collisionObjectByFrameId.end())
                {
                    continue; // already added
                }
                //_collisionObjectByFrameId[frame._spriteId] = SpritePhysicsHelper::CreateCollisionsFromSprite(frame._spriteId, GetEntity());
            }
        }
    }

    public override void Update(float elapsedTime)
    {
        if (CurrentAnimation != null)
        {
            CurrentAnimation.Update(elapsedTime);

            //var pair = _collisionObjectByFrameId[_currentAnim.CurrentFrame];
            //foreach (var collision_object_container in pair.second)
            //{
            //    Game::Instance().GetGameInfo().GetWorld().GetPhysicsWorld().ContactTest(collision_object_container);
            //}
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

    public void RemoveCollisionsFromLastFrame()
    {
        //if (_collisionObjectByFrameId.find(_lastFrameId) != _collisionObjectByFrameId.end())
        //{
        //    for (auto* collObj : _collisionObjectByFrameId[_lastFrameId])
        //    {
        //        Game::Instance().GetGameInfo().GetWorld().GetPhysicsWorld().RemoveCollisionObject(collObj);
        //    }
        //}
    }

    private void OnFrameChanged(object? sender, string frameId)
    {
        RemoveCollisionsFromLastFrame();
        //if (_collisionObjectByFrameId.find(args.ID()) != _collisionObjectByFrameId.end())
        //{
        //    SpritePhysicsHelper::AddCollisionsFromSprite(GetEntity().GetCoordinates().GetWorldMatrix().Translation(), event.ID(), _collisionObjectByFrameId[frameId]);
        //}

        _lastFrameId = frameId;
        FrameChanged?.Invoke(this, frameId);
    }

    private void OnAnimationFinished(object? sender, EventArgs args)
    {
        AnimationFinished?.Invoke(this, (Animation2d)sender);
    }

    public override void Load(JsonElement element)
    {
        throw new NotImplementedException();
    }
}
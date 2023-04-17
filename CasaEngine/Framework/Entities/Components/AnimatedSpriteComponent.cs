using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Animated Sprite")]
public class AnimatedSpriteComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.AnimatedSprite;

    public event EventHandler<string>? FrameChanged;
    public event EventHandler<Animation2d>? AnimationFinished;

    //SpriteRenderer _spriteRenderer;
    private Animation2d? _currentAnim;

    public List<Animation2d> Animations { get; } = new();

    //Dictionary<string, List<ICollisionObjectContainer>> _collisionObjectByFrameId;
    private string _lastFrameId;

    public Color Color { get; set; }
    public SpriteEffects SpriteEffect { get; set; }
    public Animation2d CurrentAnimation => _currentAnim;

    public AnimatedSpriteComponent(Entity entity) : base(entity, ComponentId)
    {
        Color = Color.White;
        SpriteEffect = SpriteEffects.None;
    }

    public void SetCurrentAnimation(Animation2d anim, bool forceReset)
    {
        if (_currentAnim != null && _currentAnim.AnimationData.Name == anim.AnimationData.Name)
        {
            if (forceReset)
            {
                _currentAnim.Reset();
            }

            return;
        }

        if (_currentAnim != null)
        {
            _currentAnim.FrameChanged -= OnFrameChanged;
            _currentAnim.AnimationFinished -= OnAnimationFinished;
        }

        _currentAnim = anim;
        _currentAnim.Reset();

        _currentAnim.FrameChanged += OnFrameChanged;
        _currentAnim.AnimationFinished += OnAnimationFinished;
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
        if (_currentAnim == null)
        {
            return "";
        }

        return _currentAnim.CurrentFrame;
    }

    public int GetCurrentFrameIndex()
    {
        if (_currentAnim == null)
        {
            return -1;
        }

        int i = 0;
        foreach (var frame in _currentAnim.Animation2dData.Frames)
        {
            if (frame._spriteId == _currentAnim.CurrentFrame)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    public void Initialize()
    {
        //_spriteRenderer = Game::Instance().GetGameComponent<SpriteRenderer>();

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
        if (_currentAnim != null)
        {
            _currentAnim.Update(elapsedTime);

            //var pair = _collisionObjectByFrameId[_currentAnim.CurrentFrame];
            //foreach (var collision_object_container in pair.second)
            //{
            //    Game::Instance().GetGameInfo().GetWorld().GetPhysicsWorld().ContactTest(collision_object_container);
            //}
        }
    }

    public void Draw()
    {
        if (_currentAnim != null)
        {
            //auto worldMatrix = GetEntity().GetCoordinates().GetWorldMatrix();
            //_spriteRenderer.AddSprite(
            //    //TODO : load all sprites in a dictionary<name, sprite>
            //    new Sprite(*Game::Instance().GetAssetManager().GetAsset<SpriteData>(_currentAnim.CurrentFrame())),
            //    worldMatrix,
            //    _color,
            //    _spriteEffect);
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
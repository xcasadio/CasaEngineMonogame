using CasaEngine.Framework.Assets.Sprites;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2d : Animation
{
    public event EventHandler<(string oldFrame, string newFrame)>? FrameChanged;

    private Animation2dData _animation2DData;
    private string _oldFrame;
    private string _currentFrame;

    public string CurrentFrame
    {
        get => _currentFrame;
        set
        {
            if (_currentFrame != value)
            {
                _oldFrame = _currentFrame;
                _currentFrame = value;
                FrameChanged?.Invoke(this, (_oldFrame, _currentFrame));
            }
        }
    }

    public Animation2dData Animation2dData => (Animation2dData)AnimationData;

    public Animation2d(Animation2dData animation2dData) : base(animation2dData)
    {
        float timeEventFired = 0.0f;
        foreach (var frame in animation2dData.Frames)
        {
            var pFrameEvent = new SetFrameEvent();
            pFrameEvent.FrameId = frame.SpriteId;
            pFrameEvent.Time = timeEventFired;
            timeEventFired += frame.Duration;
            AddEvent(pFrameEvent);
        }

        if (animation2dData.Frames.Count >= 1)
        {
            var pEndEvent = new AnimationEndEvent
            {
                Time = timeEventFired
            };
            AddEvent(pEndEvent);
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        Reset();
    }

    public override void Reset()
    {
        foreach (var @event in Events)
        {
            if (@event is SetFrameEvent pFrameEvent)
            {
                CurrentFrame = pFrameEvent.FrameId;
                break;
            }
        }

        base.Reset();
    }
}
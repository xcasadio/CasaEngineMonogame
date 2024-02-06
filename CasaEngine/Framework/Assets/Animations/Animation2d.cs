namespace CasaEngine.Framework.Assets.Animations;

public class Animation2d : Animation
{
    public event EventHandler<(Guid oldFrame, Guid newFrame)>? FrameChanged;

    private Guid _oldFrame;
    private Guid _currentFrame;

    public Guid CurrentFrame
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
            var frameEvent = new SetFrameEvent
            {
                FrameId = frame.SpriteId,
                Time = timeEventFired
            };
            timeEventFired += frame.Duration;
            AddEvent(frameEvent);
        }

        if (animation2dData.Frames.Count >= 1)
        {
            var animationEndEvent = new AnimationEndEvent
            {
                Time = timeEventFired
            };
            AddEvent(animationEndEvent);
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
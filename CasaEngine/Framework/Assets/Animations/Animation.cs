using System.Diagnostics;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation
{
    protected readonly List<AnimationEvent> Events = new();
    private bool _isInitialized;

    public event EventHandler? AnimationFinished;

    public AnimationData AnimationData { get; }

    public float CurrentTime { get; set; }

    public float TotalTime => Events.Count >= 1 ? Events[^1].Time : 0.0f;

    protected Animation(AnimationData animationData)
    {
        AnimationData = animationData;
    }

    public virtual void Initialize()
    {
        _isInitialized = true;
    }

    public void Update(float elapsedTime)
    {
        Debug.Assert(_isInitialized, "Animation::Update() : call Initialize before Update()");

        var totalTime = TotalTime;
        if (totalTime == 0.0f)
        {
            return;
        }

        bool isFinished = false;
        float lastTime = CurrentTime;
        CurrentTime += elapsedTime;
        float currentTime = CurrentTime;

        if (AnimationData.AnimationType == AnimationType.Loop)
        {
            while (CurrentTime > totalTime)
            {
                CurrentTime -= totalTime;
                //isFinished = true;
            }
            currentTime = CurrentTime;
        }
        else if (AnimationData.AnimationType == AnimationType.Once)
        {
            if (CurrentTime > totalTime)
            {
                CurrentTime = totalTime;
                currentTime = CurrentTime;
                isFinished = true;
            }
        }
        else if (AnimationData.AnimationType == AnimationType.PingPong)
        {
            int pingPongState = 0;

            while (currentTime > totalTime)
            {
                currentTime -= totalTime;
                pingPongState = 1 - pingPongState;
            }

            if (pingPongState == 1)
            {
                currentTime = totalTime - currentTime;
            }
        }
        else
        {
            throw new ArgumentException("animation type is not supported");
        }

        if (isFinished)
        {
            AnimationFinished?.Invoke(this, EventArgs.Empty);
        }

        // _events must be sorted by time
        foreach (var @event in Events)
        {
            if (lastTime >= @event.Time && @event.Time < currentTime)
            {
                @event.Activate(this);
            }
        }
    }

    public void AddEvent(AnimationEvent animationEvent)
    {
        Events.Add(animationEvent);
    }

    public virtual void Reset()
    {
        CurrentTime = 0.0f;
    }

#if EDITOR
    void RemoveEvent(AnimationEvent animationEvent)
    {
        Events.Remove(animationEvent);
    }
#endif

}
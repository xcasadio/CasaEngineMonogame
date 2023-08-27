using System.Diagnostics;
using CasaEngine.Core.Logger;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation
{
    protected readonly List<AnimationEvent> Events = new();
    protected readonly HashSet<AnimationEvent> ActivatedEvents = new();
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
                ActivatedEvents.Clear();
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
                ActivatedEvents.Clear();
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
            LogManager.Instance.WriteLineTrace($"Animation ended : {AnimationData.AssetInfo.Name}");
            AnimationFinished?.Invoke(this, EventArgs.Empty);
        }

        // _events must be sorted by time
        foreach (var @event in Events)
        {
            if (ActivatedEvents.Contains(@event))
            {
                continue;
            }

            if (@event.Time <= currentTime)
            {
                @event.Activate(this);
                ActivatedEvents.Add(@event);
            }
            else
            {
                break;
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
        ActivatedEvents.Clear();
    }

#if EDITOR
    void RemoveEvent(AnimationEvent animationEvent)
    {
        Events.Remove(animationEvent);
    }
#endif

}
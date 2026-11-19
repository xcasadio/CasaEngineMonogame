namespace CasaEngine.Framework.Cutscenes;

/// <summary>What a <see cref="CutsceneDirector"/> reports when an action starts or ends: the action and the play clock.</summary>
public readonly struct CutsceneActionNotification
{
    public CutsceneActionNotification(CutsceneActionData action, double elapsedSeconds, long elapsedFrames)
    {
        Action = action;
        ElapsedSeconds = elapsedSeconds;
        ElapsedFrames = elapsedFrames;
    }

    /// <summary>The action that starts or ends, as it is in the asset being played.</summary>
    public CutsceneActionData Action { get; }

    /// <summary>The value of <see cref="CutsceneDirector.PlayElapsedSeconds"/> at that moment.</summary>
    public double ElapsedSeconds { get; }

    /// <summary>The value of <see cref="CutsceneDirector.PlayElapsedFrames"/> at that moment.</summary>
    public long ElapsedFrames { get; }
}

/// <summary>Receives the notification of an action of a play.</summary>
public delegate void CutsceneActionNotificationHandler(in CutsceneActionNotification notification);

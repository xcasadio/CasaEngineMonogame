namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>
/// State of the frozen frame: keeps the image of the previous frame and shows it frozen
/// (E19.TR-4a, D-TR4A-6). Knows nothing about MonoGame types.
/// </summary>
/// <remarks>
/// SKELETON (E19.TR-4a, T1a): every member throws <see cref="NotImplementedException"/> until T1b.
/// </remarks>
public sealed class FrozenFrameService
{
    /// <summary>Keeps the last composed frame; false by default.</summary>
    public bool RetainLastFrame
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <summary>Asks to keep the image of the previous frame; idempotent while a request waits.</summary>
    public void RequestCapture() => throw new NotImplementedException();

    public bool CaptureRequested => throw new NotImplementedException();

    public FrozenFrameCaptureState LastCapture => throw new NotImplementedException();

    /// <summary>Number of completed captures; monotone.</summary>
    public long CaptureCount => throw new NotImplementedException();

    public bool HasImage => throw new NotImplementedException();

    public int CapturedWidth => throw new NotImplementedException();

    public int CapturedHeight => throw new NotImplementedException();

    public void ShowBase() => throw new NotImplementedException();

    public void HideBase() => throw new NotImplementedException();

    public bool BaseVisible => throw new NotImplementedException();

    /// <summary>The drawing test: BaseVisible and HasImage.</summary>
    public bool IsBaseDrawable => throw new NotImplementedException();

    /// <summary>The registration rule: BaseVisible and (HasImage or CaptureRequested).</summary>
    public bool ShouldRegisterBase => throw new NotImplementedException();

    /// <summary>Hides and drops the image, forgets a pending request; keeps <see cref="CaptureCount"/>.</summary>
    public void Release() => throw new NotImplementedException();

    internal void CompleteCapture(int width, int height) => throw new NotImplementedException();

    internal void FailCapture() => throw new NotImplementedException();
}

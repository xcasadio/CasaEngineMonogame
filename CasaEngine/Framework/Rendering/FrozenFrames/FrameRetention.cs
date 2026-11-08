namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>
/// GPU side of the frozen frame: the retained and captured targets (E19.TR-4a, D-TR4A-3).
/// </summary>
/// <remarks>SKELETON (E19.TR-4a, T1a): throws until T1b.</remarks>
internal sealed class FrameRetention : IDisposable
{
    internal FrameRetention(FrozenFrameService service) => throw new NotImplementedException();

    /// <summary>The view whose frame is retained (the active view).</summary>
    internal RenderView TargetView
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <summary>The retention option, followed from <see cref="FrozenFrameService.RetainLastFrame"/>.</summary>
    internal bool Enabled
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public void Dispose() => throw new NotImplementedException();
}

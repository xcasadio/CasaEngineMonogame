using CasaEngine.Engine.Input.InputDeviceStateProviders;
using MGUI.Shared.Input;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Caches one window input snapshot per frame so all consumers observe the same raw state.
/// </summary>
public sealed class FrameCachedWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider
{
    private readonly IWindowInputSource _inner;
    private long _nextFrameId;
    private WindowInputSnapshot _currentSnapshot = WindowInputSnapshot.Empty;
    private bool _hasCurrentSnapshot;

    public FrameCachedWindowInputSource(IWindowInputSource inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public long CurrentFrameId => _currentSnapshot.FrameId;

    public WindowInputSnapshot CaptureFrameInput()
    {
        var snapshot = _inner.GetSnapshot();
        long frameId = Interlocked.Increment(ref _nextFrameId);
        _currentSnapshot = snapshot with { FrameId = frameId };
        _hasCurrentSnapshot = true;
        return _currentSnapshot;
    }

    public WindowInputSnapshot GetSnapshot()
    {
        return _hasCurrentSnapshot ? _currentSnapshot : CaptureFrameInput();
    }

    public KeyboardState GetKeyboardState() => GetSnapshot().KeyboardState;

    public MouseState GetMouseState() => GetSnapshot().MouseState;

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}
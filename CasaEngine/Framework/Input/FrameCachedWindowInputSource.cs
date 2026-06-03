using CasaEngine.Engine.Input.Providers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Caches one window input snapshot per frame so all consumers observe the same raw state.
/// </summary>
public sealed class FrameCachedWindowInputSource : IWindowInputSource, IRawInputSource, IKeyboardStateProvider, IMouseStateProvider, IWindowTextInputSource, IWindowFileDropSource
{
    private readonly IWindowInputSource _inner;
    private long _nextFrameId;
    private WindowInputSnapshot _currentSnapshot = WindowInputSnapshot.Empty;
    private bool _hasCurrentSnapshot;
    private WindowInputSnapshot _queuedSnapshotOverride = WindowInputSnapshot.Empty;
    private bool _hasQueuedSnapshotOverride;

    public FrameCachedWindowInputSource(IWindowInputSource inner, bool captureAutomatically = true)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        CaptureAutomatically = captureAutomatically;
    }

    public bool CaptureAutomatically { get; }

    public long CurrentFrameId => _currentSnapshot.FrameId;

    public void QueueNextSnapshot(WindowInputSnapshot snapshot)
    {
        _queuedSnapshotOverride = snapshot;
        _hasQueuedSnapshotOverride = true;
    }

    public WindowInputSnapshot CaptureFrameInput()
    {
        var snapshot = _hasQueuedSnapshotOverride ? _queuedSnapshotOverride : _inner.GetSnapshot();
        _hasQueuedSnapshotOverride = false;
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

    public void DrainTextInput(IKeyboardTextInputSink sink)
    {
        if (_inner is IWindowTextInputSource textInputSource)
        {
            textInputSource.DrainTextInput(sink);
        }
    }

    public void DrainDroppedFiles(ICollection<string> filePaths)
    {
        if (_inner is IWindowFileDropSource fileDropSource)
        {
            fileDropSource.DrainDroppedFiles(filePaths);
        }
    }

    KeyboardState IKeyboardStateProvider.GetState() => GetKeyboardState();

    MouseState IMouseStateProvider.GetState() => GetMouseState();
}
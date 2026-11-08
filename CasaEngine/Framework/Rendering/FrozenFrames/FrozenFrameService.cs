using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>
/// State of the frozen frame: keeps the image of the previous frame and shows it frozen
/// (E19.TR-4a, D-TR4A-6). Knows nothing about MonoGame types.
/// </summary>
/// <remarks>
/// <para>
/// Contract: the image kept is the one of the <c>Draw</c> of the <b>previous</b> tour, user interface and
/// veils included. The request and the display are posed in the same <c>Update</c>:
/// <c>RequestCapture(); ShowBase();</c>.
/// </para>
/// <para>
/// State persists until <see cref="HideBase"/> or <see cref="Release"/> (ADR-0071). Forgetting a
/// <see cref="Release"/> freezes the screen for good: releasing is a duty of the caller; the engine never
/// releases by itself, the image must survive a change of world.
/// </para>
/// </remarks>
public sealed class FrozenFrameService
{
    private bool _captureRequested;
    private bool _hasImage;
    private bool _baseVisible;
    private int _capturedWidth;
    private int _capturedHeight;
    private long _captureCount;
    private FrozenFrameCaptureState _lastCapture;

    /// <summary>Keeps the last composed frame; false by default.</summary>
    public bool RetainLastFrame { get; set; }

    /// <summary>Asks to keep the image of the previous frame; idempotent while a request waits.</summary>
    public void RequestCapture() => _captureRequested = true;

    public bool CaptureRequested => _captureRequested;

    public FrozenFrameCaptureState LastCapture => _lastCapture;

    /// <summary>Number of completed captures; monotone.</summary>
    public long CaptureCount => _captureCount;

    public bool HasImage => _hasImage;

    public int CapturedWidth => _capturedWidth;

    public int CapturedHeight => _capturedHeight;

    public void ShowBase() => _baseVisible = true;

    public void HideBase() => _baseVisible = false;

    public bool BaseVisible => _baseVisible;

    /// <summary>The drawing test: BaseVisible and HasImage.</summary>
    public bool IsBaseDrawable => _baseVisible && _hasImage;

    /// <summary>
    /// The registration rule: BaseVisible and (HasImage or CaptureRequested). The component decides in its
    /// <c>Update</c>, before the capture made in the <c>Render</c> of the same tour, when <see cref="HasImage"/>
    /// is not yet true: a pending request is enough to register (R-TR5-1).
    /// </summary>
    public bool ShouldRegisterBase => _baseVisible && (_hasImage || _captureRequested);

    /// <summary>Hides and drops the image, forgets a pending request; keeps <see cref="CaptureCount"/>.</summary>
    public void Release()
    {
        _baseVisible = false;
        _hasImage = false;
        _capturedWidth = 0;
        _capturedHeight = 0;
        _captureRequested = false;
    }

    internal void CompleteCapture(int width, int height)
    {
        _hasImage = true;
        _capturedWidth = width;
        _capturedHeight = height;
        _captureRequested = false;
        _captureCount++;
        _lastCapture = FrozenFrameCaptureState.Captured;
    }

    /// <summary>
    /// The waiting request found no retained image: drops any earlier image (never show one unrelated to the
    /// request) and reports <see cref="FrozenFrameCaptureState.Unavailable"/>. Called once per request.
    /// </summary>
    internal void FailCapture()
    {
        _hasImage = false;
        _capturedWidth = 0;
        _capturedHeight = 0;
        _captureRequested = false;
        _lastCapture = FrozenFrameCaptureState.Unavailable;
        Logs.WriteWarning(
            "[FrozenFrame] Capture unavailable: no retained frame (first frame, resize, ineligible view or option off).");
    }
}

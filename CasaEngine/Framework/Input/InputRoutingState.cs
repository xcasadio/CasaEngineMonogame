using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Snapshot of the engine input routing decision for the current frame.
/// Priority order is always: modal UI, captured view, pointer-hovered view,
/// keyboard-focused view, then fallback input.
/// </summary>
public readonly record struct InputRoutingState(
    ViewId TargetViewId,
    ViewId PointerViewId,
    ViewId KeyboardFocusViewId,
    ViewId ModalViewId,
    ViewId CaptureViewId,
    ViewId UIPointerCaptureViewId,
    InputRoutingReason Reason)
{
    public static InputRoutingState Empty { get; } = new(
        ViewId.Empty,
        ViewId.Empty,
        ViewId.Empty,
        ViewId.Empty,
        ViewId.Empty,
        ViewId.Empty,
        InputRoutingReason.None);
}

public enum InputRoutingReason
{
    None,
    Modal,
    Capture,
    UIPointerCapture,
    Pointer,
    KeyboardFocus,
    Fallback,
}
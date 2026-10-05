using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

/// <summary>
/// Owner of the device scissor rectangle between two frames of the UI (ADR-0054). MonoGame leaves the scissor at the value of the
/// last device reset when the window is resized by the user, so the first clip of the UI would intersect with a stale rectangle.
/// </summary>
internal static class UiDeviceScissor
{
    /// <summary>
    /// Sets, unconditionally, the device scissor to the whole back buffer. Between two frames no caller of the runtime holds a
    /// scissor of its own (the per-view graphics state snapshot takes it at the start of each view and gives it back at the end),
    /// so any other value is stale.
    /// </summary>
    public static void ResetToBackBuffer(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        PresentationParameters parameters = device.PresentationParameters;
        device.ScissorRectangle = new Rectangle(0, 0, parameters.BackBufferWidth, parameters.BackBufferHeight);
    }
}

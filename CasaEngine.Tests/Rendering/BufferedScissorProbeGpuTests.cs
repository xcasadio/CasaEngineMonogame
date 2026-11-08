using CasaEngine.Framework.Rendering;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// What a buffered view (ADR-0087) relies on: binding a <see cref="RenderTargetSurface"/> sets the device scissor to the
/// target, whatever the back-buffer scissor was, so the UI clips and the above-UI fade drawn into the target are bounded
/// by the target, and restoring it gives the back-buffer scissor back. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class BufferedScissorProbeGpuTests
{
    [GpuFact]
    public void BindingABufferTarget_SetsTheScissorToTheTarget_AndRestoringGivesTheBackBufferOneBack()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            using var surface = new RenderTargetSurface(device, 640, 480);
            device.ScissorRectangle = new Rectangle(0, 0, 100, 50);

            surface.Apply(device);
            Assert.Equal(new Rectangle(0, 0, 640, 480), device.ScissorRectangle);

            surface.Restore(device);
            var backBuffer = device.PresentationParameters;
            Assert.Equal(new Rectangle(0, 0, backBuffer.BackBufferWidth, backBuffer.BackBufferHeight), device.ScissorRectangle);
        });
    }
}

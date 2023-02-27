using Microsoft.Xna.Framework.Graphics;
using SharpDX.Direct3D9;
using System.Runtime.InteropServices;
using DeviceType = SharpDX.Direct3D9.DeviceType;
using PresentInterval = SharpDX.Direct3D9.PresentInterval;
using Texture = SharpDX.Direct3D9.Texture;

namespace Microsoft.Xna.Framework.Internals
{
    internal class D3D9Device : IDisposable
    {
        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();

        DeviceEx _device;
        Direct3DEx _direct3D;
        bool _disposed;

        public D3D9Device()
        {
            // Create Direct3DEx device on Windows Vista/7/8 with a display configured to use the Windows Display Driver Model (WDDM). Use Direct3D on any other platform.
            _direct3D = new Direct3DEx();
            _device = new DeviceEx(_direct3D, 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve, new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                PresentationInterval = PresentInterval.Default,
                // The device back buffer is not used.
                BackBufferFormat = Format.Unknown,
                BackBufferWidth = 1,
                BackBufferHeight = 1,
                // Use dummy window handle.
                DeviceWindowHandle = GetDesktopWindow()
            });
        }

        #region Dispose

        ~D3D9Device() { Dispose(false); }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources.
                    if (_device != null)
                    {
                        _device.Dispose();
                        _device = null;
                    }
                    if (_direct3D != null)
                    {
                        _direct3D.Dispose();
                        _direct3D = null;
                    }
                }

                // Release unmanaged resources.
                _disposed = true;
            }
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #endregion

        public Texture GetSharedTexture(Texture2D renderTarget)
        {
            ThrowIfDisposed();
            if (renderTarget == null)
            {
                return null;
            }

            var handle = renderTarget.GetSharedHandle();
            if (handle == IntPtr.Zero)
            {
                throw new ArgumentException("Unable to access resource. The texture needs to be created as a shared resource.", nameof(renderTarget));
            }

            var format = renderTarget.Format switch
            {
                SurfaceFormat.Bgr32 => Format.X8R8G8B8,
                SurfaceFormat.Bgra32 => Format.A8R8G8B8,
                _ => throw new ArgumentException("Unexpected surface format. Supported formats are: SurfaceFormat.Bgr32, SurfaceFormat.Bgra32.", nameof(renderTarget)),
            };
            return new Texture(_device, renderTarget.Width, renderTarget.Height, 1, Usage.RenderTarget, format, Pool.Default, ref handle);
        }
    }
}
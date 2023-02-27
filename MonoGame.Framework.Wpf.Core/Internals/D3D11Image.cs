using Microsoft.Xna.Framework.Graphics;
using System.Windows;
using System.Windows.Interop;
using Texture = SharpDX.Direct3D9.Texture;

namespace Microsoft.Xna.Framework.Internals
{
    internal class D3D11Image : D3DImage, IDisposable
    {
        static readonly object _lock = new object();

        // Use a Direct3D 9 device for interoperability. The device is shared by all D3D11Images.
        static D3D9Device _d3D9Device;
        static int _referenceCount;

        Texture _backBuffer;
        bool _disposed;

        public D3D11Image() => InitializeD3D9();

        #region Dispose

        ~D3D11Image() { Dispose(false); }

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
                    SetBackBuffer(null);
                    if (_backBuffer != null)
                    {
                        _backBuffer.Dispose();
                        _backBuffer = null;
                    }
                }
                // Release unmanaged resources.
                UninitializeD3D9();
                _disposed = true;
            }
        }

        static void InitializeD3D9()
        {
            lock (_lock)
            {
                _referenceCount++;
                if (_referenceCount == 1)
                {
                    _d3D9Device = new D3D9Device();
                }
            }
        }

        static void UninitializeD3D9()
        {
            lock (_lock)
            {
                _referenceCount--;
                if (_referenceCount == 0)
                {
                    _d3D9Device.Dispose();
                    _d3D9Device = null;
                }
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

        public void Invalidate()
        {
            ThrowIfDisposed();
            if (_backBuffer != null)
            {
                Lock();
                AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
                Unlock();
            }
        }

        public void SetBackBuffer(Texture2D texture)
        {
            ThrowIfDisposed();
            var previousBackBuffer = _backBuffer;
            // Create shared texture on Direct3D 9 device.
            _backBuffer = _d3D9Device.GetSharedTexture(texture);
            if (_backBuffer != null)
            {
                using var surface = _backBuffer.GetSurfaceLevel(0);
                Lock();
                // Set texture as new back buffer.
                SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
                Unlock();
            }
            else
            {
                Lock();
                // Reset back buffer.
                SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                Unlock();
            }
            previousBackBuffer?.Dispose();
        }
    }
}
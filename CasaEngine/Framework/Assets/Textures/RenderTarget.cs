
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Graphics;
using Size = CasaEngine.Core.Helpers.Size;

namespace CasaEngine.Framework.Assets.Textures
{
    public sealed class RenderTarget : Texture
    {

        public struct RenderTargetBinding
        {
            internal Microsoft.Xna.Framework.Graphics.RenderTargetBinding[] InternalBinding;
            public RenderTarget[] RenderTargets;

            public static bool operator ==(RenderTargetBinding x, RenderTargetBinding y)
            {
                return x.InternalBinding == y.InternalBinding;
            } // Equal

            public static bool operator !=(RenderTargetBinding x, RenderTargetBinding y)
            {
                return x.InternalBinding != y.InternalBinding;
            } // Not Equal

            public override bool Equals(object obj)
            {
                return obj is RenderTargetBinding && this == (RenderTargetBinding)obj;
            } // Equals

            public override int GetHashCode()
            {
                return InternalBinding.GetHashCode() ^ InternalBinding.GetHashCode();
            } // GetHashCode

        } // RenderTargetBinding

        public enum AntialiasingType
        {
            System,
            NoAntialiasing,
            TwoSamples,
            FourSamples,
            EightSamples,
            SixtySamples
        } // AntialiasingType

        // XNA Render target.
        // Why don't use the derived xnaTexture? Good question. I don't remember why I do it.
        private RenderTarget2D _renderTarget;

        // Make sure we don't call xnaTexture before resolving for the first time!
        private bool _alreadyResolved;

        // Indicates if this render target is currently used and if its information has to be preserved.
        private bool _looked;

        // Remember the last render targets we set. We can enable up to four render targets at once.
        private static readonly RenderTarget[] currentRenderTarget = new RenderTarget[4];

        private RenderTargetBinding? _renderTargetBinding;

        public override Texture2D Resource
        {
            get
            {
                if (_alreadyResolved)
                {
                    return _renderTarget;
                }

                throw new InvalidOperationException("Render Target: Unable to return render target. Render target not resolved.");
            }
        } // Resource

        public SurfaceFormat SurfaceFormat { get; private set; }

        public DepthFormat DepthFormat { get; private set; }

        public AntialiasingType Antialiasing { get; private set; }

        public bool MipMap { get; private set; }

        public static RenderTarget[] CurrentRenderTarget => currentRenderTarget;

        public RenderTarget(GraphicsDevice graphicsDevice, Size size,
            SurfaceFormat surfaceFormat, DepthFormat depthFormat,
            AntialiasingType antialiasingType = AntialiasingType.NoAntialiasing, bool mipMap = false)
            : base(graphicsDevice)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = surfaceFormat;
            DepthFormat = depthFormat;
            Antialiasing = antialiasingType;
            MipMap = mipMap;

            Create();
            GraphicsDevice.DeviceReset += OnScreenSizeChanged;
        } // RenderTarget

        public RenderTarget(
            GraphicsDevice graphicsDevice,
            Size size,
            SurfaceFormat surfaceFormat = SurfaceFormat.Color,
            bool hasDepthBuffer = true,
            AntialiasingType antialiasingType = AntialiasingType.NoAntialiasing,
            bool mipMap = false)
            : base(graphicsDevice)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = surfaceFormat;
            DepthFormat = hasDepthBuffer ? DepthFormat.Depth24 : DepthFormat.None;
            Antialiasing = antialiasingType;
            MipMap = mipMap;

            Create();
            GraphicsDevice.DeviceReset += OnScreenSizeChanged;
        } // RenderTarget

        private void Create()
        {
            try
            {
                // Create render target of specified size.
                // On Xbox 360, the render target will discard contents. On PC, the render target will discard if multisampling is enabled, and preserve the contents if not.
                // I use RenderTargetUsage.PlatformContents to be little more performance friendly with PC.
                // But I assume that the system works in DiscardContents mode so that an XBOX 360 implementation works.
                // What I lose, mostly nothing, because I made my own ZBuffer texture and the stencil buffer is deleted no matter what I do.
                _renderTarget = new RenderTarget2D(GraphicsDevice, Width, Height, MipMap, SurfaceFormat,
                    DepthFormat, CalculateMultiSampleQuality(Antialiasing), RenderTargetUsage.PlatformContents);
                _alreadyResolved = true;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Render target creation failed", e);
            }
        } // Create

        protected override void DisposeManagedResources()
        {
            base.DisposeManagedResources();
            GraphicsDevice.DeviceReset -= OnScreenSizeChanged;
            _renderTarget.Dispose();
        } // DisposeManagedResources

        private void OnScreenSizeChanged(object sender, EventArgs e)
        {
            // Just do it. Sometimes the render targets are not automatically recreated. This seems to happen with floating point surface format.
            //if (Size == Size.FullScreen || Size == Size.HalfScreen || Size == Size.QuarterScreen ||
            //    Size == Size.SplitFullScreen || Size == Size.SplitHalfScreen || Size == Size.SplitQuarterScreen)
            {
                // Render Targets don't use content managers.
                _renderTarget.Dispose();
                OnDeviceReset(GraphicsDevice);
            }
        } // OnScreenSizeChanged

        internal override void OnDeviceReset(GraphicsDevice device)
        {
            Create();
            // Redo the bindings
            if (_renderTargetBinding.HasValue)
            {
                for (var i = 0; i < _renderTargetBinding.Value.InternalBinding.Length; i++)
                {
                    _renderTargetBinding.Value.InternalBinding[i] =
                        new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(_renderTargetBinding.Value.RenderTargets[i]._renderTarget);
                }
            }
        } // RecreateResource

        internal static int CalculateMultiSampleQuality(AntialiasingType antialiasingTypeType)
        {
            switch (antialiasingTypeType)
            {
                case AntialiasingType.NoAntialiasing:
                    return 0;
                case AntialiasingType.System:
                    return Game.Engine.Instance.GraphicsSettings.MultiSampleQuality;
                case AntialiasingType.TwoSamples:
                    return 2;
                case AntialiasingType.FourSamples:
                    return 4;
                case AntialiasingType.EightSamples:
                    return 8;
                case AntialiasingType.SixtySamples:
                    return 16;
                default:
                    throw new ArgumentException("Render Target error. Antialiasing type doesn't exist (probably a bug).");
            }
        } // CalculateMultiSampleQuality

        public void EnableRenderTarget()
        {
            if (currentRenderTarget[0] != null)
            {
                throw new InvalidOperationException("Render Target: unable to set render target. Another render target is still set. If you want to set multiple render targets use the static method called EnableRenderTargets.");
            }

            GraphicsDevice.SetRenderTarget(_renderTarget);
            currentRenderTarget[0] = this;
            _alreadyResolved = false;
        } // EnableRenderTarget

        public static void EnableRenderTargets(GraphicsDevice graphicsDevice, RenderTargetBinding renderTargetBinding)
        {
            if (currentRenderTarget[0] != null)
            {
                throw new InvalidOperationException("Render Target: unable to set render target. Another render target is still set.");
            }

            for (var i = 0; i < renderTargetBinding.RenderTargets.Length; i++)
            {
                currentRenderTarget[i] = renderTargetBinding.RenderTargets[i];
                renderTargetBinding.RenderTargets[i]._alreadyResolved = false;
            }
            try
            {
                graphicsDevice.SetRenderTargets(renderTargetBinding.InternalBinding);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Render Target. Unable to bind the render targets.", e);
            }
        } // EnableRenderTargets

        public void Clear(Color clearColor)
        {
            if (currentRenderTarget[0] != this)
            {
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            }

            if (DepthFormat == DepthFormat.None)
            {
                GraphicsDevice.Clear(clearColor);
            }
            else if (DepthFormat == DepthFormat.Depth24Stencil8)
            {
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1.0f, 0);
            }
            else
            {
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, clearColor, 1.0f, 0);
            }
        } // Clear

        public static void ClearCurrentRenderTargets(Color clearColor)
        {
            if (currentRenderTarget[0] == null)
            {
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            }

            currentRenderTarget[0].Clear(clearColor);
        } // Clear

        public void DisableRenderTarget()
        {
            // Make sure this render target is currently set!
            if (currentRenderTarget[0] != this)
            {
                throw new InvalidOperationException("Render Target: Cannot call disable to a render target without first setting it.");
            }
            if (currentRenderTarget[1] != null)
            {
                throw new InvalidOperationException("Render Target: There are multiple render targets enabled. Use RenderTarget.BackToBackBuffer instead.");
            }

            _alreadyResolved = true;
            currentRenderTarget[0] = null;
            GraphicsDevice.SetRenderTarget(null);
        } // DisableRenderTarget

        public static void DisableCurrentRenderTargets(GraphicsDevice graphicsDevice)
        {
            for (var i = 0; i < 4; i++)
            {
                if (currentRenderTarget[i] != null)
                {
                    currentRenderTarget[i]._alreadyResolved = true;
                }

                currentRenderTarget[i] = null;
            }
            graphicsDevice.SetRenderTarget(null);
        } // DisableCurrentRenderTargets

        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2)
        {
            var renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2._renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2 }
            };
            renderTarget1._renderTargetBinding = renderTargetsBinding;
            renderTarget2._renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets

        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2, RenderTarget renderTarget3)
        {
            var renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget3._renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2, renderTarget3 }
            };
            renderTarget1._renderTargetBinding = renderTargetsBinding;
            renderTarget2._renderTargetBinding = renderTargetsBinding;
            renderTarget3._renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets

        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2, RenderTarget renderTarget3, RenderTarget renderTarget4)
        {
            var renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget3._renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget4._renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2, renderTarget3, renderTarget4 }
            };
            renderTarget1._renderTargetBinding = renderTargetsBinding;
            renderTarget2._renderTargetBinding = renderTargetsBinding;
            renderTarget3._renderTargetBinding = renderTargetsBinding;
            renderTarget4._renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets

        // A pool of all render targets.
        private static readonly List<RenderTarget> RenderTargets = new(0);

        public static RenderTarget Fetch(GraphicsDevice graphicsDevice, Size size, SurfaceFormat surfaceFormat, DepthFormat depthFormat, AntialiasingType antialiasingType, bool mipMap = false)
        {
            RenderTarget renderTarget;
            for (var i = 0; i < RenderTargets.Count; i++)
            {
                renderTarget = RenderTargets[i];
                if (renderTarget.Size == size && renderTarget.SurfaceFormat == surfaceFormat &&
                    renderTarget.DepthFormat == depthFormat && renderTarget.Antialiasing == antialiasingType && renderTarget.MipMap == mipMap && !renderTarget._looked)
                {
                    renderTarget._looked = true;
                    return renderTarget;
                }
            }
            // If there is not one unlook or present we create one.
            renderTarget = new RenderTarget(graphicsDevice, size, surfaceFormat, depthFormat, antialiasingType, mipMap);
            RenderTargets.Add(renderTarget);
            renderTarget._looked = true;
            return renderTarget;
        } // Fetch

        public static void Release(RenderTarget rendertarget)
        {
            if (rendertarget == null)
            {
                return;
            }

            for (var i = 0; i < RenderTargets.Count; i++)
            {
                if (rendertarget == RenderTargets[i])
                {
                    rendertarget._looked = false;
                    return;
                }
            }
            // If not do nothing.
            //throw new ArgumentException("Render Target: Cannot release render target. The render target is not present in the pool.");
        } // Release

        public static void ClearRenderTargetPool()
        {
            for (var i = 0; i < RenderTargets.Count; i++)
            {
                RenderTargets[i].Dispose();
            }

            RenderTargets.Clear();
        } // ClearRenderTargetPool

        // A pool of all multiple render targets.
        private static readonly List<RenderTargetBinding> MultipleRenderTargets = new(0);

        public static RenderTargetBinding Fetch(GraphicsDevice graphicsDevice, Size size, SurfaceFormat surfaceFormat1, DepthFormat depthFormat, SurfaceFormat surfaceFormat2)
        {
            RenderTargetBinding renderTargetBinding;
            for (var i = 0; i < MultipleRenderTargets.Count; i++)
            {
                renderTargetBinding = MultipleRenderTargets[i];
                // If is a multiple render target of three render targets.
                if (renderTargetBinding.RenderTargets.Length == 2)
                {
                    if (renderTargetBinding.RenderTargets[0].Size == size && renderTargetBinding.RenderTargets[0].SurfaceFormat == surfaceFormat1 &&
                        renderTargetBinding.RenderTargets[0].DepthFormat == depthFormat &&
                        renderTargetBinding.RenderTargets[1].SurfaceFormat == surfaceFormat2 &&
                        !renderTargetBinding.RenderTargets[0]._looked)
                    {
                        renderTargetBinding.RenderTargets[0]._looked = true;
                        return renderTargetBinding;
                    }
                }
            }
            // If there is not one unlook or present we create one.
            var renderTarget1 = new RenderTarget(graphicsDevice, size, surfaceFormat1, depthFormat);
            var renderTarget2 = new RenderTarget(graphicsDevice, size, surfaceFormat2, false);
            renderTargetBinding = BindRenderTargets(renderTarget1, renderTarget2);
            MultipleRenderTargets.Add(renderTargetBinding);
            renderTargetBinding.RenderTargets[0]._looked = true;
            return renderTargetBinding;
        } // Fetch

        public static RenderTargetBinding Fetch(GraphicsDevice graphicsDevice, Size size, SurfaceFormat surfaceFormat1, DepthFormat depthFormat, SurfaceFormat surfaceFormat2, SurfaceFormat surfaceFormat3)
        {
            RenderTargetBinding renderTargetBinding;
            for (var i = 0; i < MultipleRenderTargets.Count; i++)
            {
                renderTargetBinding = MultipleRenderTargets[i];
                // If is a multiple render target of three render targets.
                if (renderTargetBinding.RenderTargets.Length == 3)
                {
                    if (renderTargetBinding.RenderTargets[0].Size == size && renderTargetBinding.RenderTargets[0].SurfaceFormat == surfaceFormat1 &&
                        renderTargetBinding.RenderTargets[0].DepthFormat == depthFormat &&
                        renderTargetBinding.RenderTargets[1].SurfaceFormat == surfaceFormat2 &&
                        renderTargetBinding.RenderTargets[2].SurfaceFormat == surfaceFormat3 &&
                        !renderTargetBinding.RenderTargets[0]._looked)
                    {
                        renderTargetBinding.RenderTargets[0]._looked = true;
                        return renderTargetBinding;
                    }
                }
            }
            // If there is not one unlook or present we create one.
            var renderTarget1 = new RenderTarget(graphicsDevice, size, surfaceFormat1, depthFormat);
            var renderTarget2 = new RenderTarget(graphicsDevice, size, surfaceFormat2, false);
            var renderTarget3 = new RenderTarget(graphicsDevice, size, surfaceFormat3, false);
            renderTargetBinding = BindRenderTargets(renderTarget1, renderTarget2, renderTarget3);
            MultipleRenderTargets.Add(renderTargetBinding);
            renderTargetBinding.RenderTargets[0]._looked = true;
            return renderTargetBinding;
        } // Fetch

        public static void Release(RenderTargetBinding renderTargetBinding)
        {
            for (var i = 0; i < MultipleRenderTargets.Count; i++)
            {
                if (renderTargetBinding == MultipleRenderTargets[i])
                {
                    renderTargetBinding.RenderTargets[0]._looked = false;
                    return;
                }
            }
            // If not do nothing.
            //throw new ArgumentException("Render Target: Cannot release multiple render target. The multiple render target is not present in the pool.");
        } // Release

        public static void ClearMultpleRenderTargetPool()
        {
            for (var i = 0; i < MultipleRenderTargets.Count; i++)
            {
                for (var j = 0; j < MultipleRenderTargets[i].RenderTargets.Length; j++)
                {
                    MultipleRenderTargets[i].RenderTargets[j].Dispose();
                }
            }
            MultipleRenderTargets.Clear();
        } // ClearMultpleRenderTargetPool

    } // RenderTarget
} // CasaEngine.Asset
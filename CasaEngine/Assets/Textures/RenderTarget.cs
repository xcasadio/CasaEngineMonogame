
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
using CasaEngine.Game;
using Size = XNAFinalEngine.Helpers.Size;

namespace CasaEngine.Asset
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

            public override bool Equals(Object obj)
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
        private RenderTarget2D renderTarget;

        // Make sure we don't call xnaTexture before resolving for the first time!
        private bool alreadyResolved;

        // Indicates if this render target is currently used and if its information has to be preserved.
        private bool looked;

        // Remember the last render targets we set. We can enable up to four render targets at once.
        private static readonly RenderTarget[] currentRenderTarget = new RenderTarget[4];

        private RenderTargetBinding? renderTargetBinding;



        public override Texture2D Resource
        {
            get
            {
                if (alreadyResolved)
                    return renderTarget;
                throw new InvalidOperationException("Render Target: Unable to return render target. Render target not resolved.");
            }
        } // Resource

        public SurfaceFormat SurfaceFormat { get; private set; }

        public DepthFormat DepthFormat { get; private set; }

        public AntialiasingType Antialiasing { get; private set; }

        public bool MipMap { get; private set; }

        public static RenderTarget[] CurrentRenderTarget => currentRenderTarget;


        public RenderTarget(GraphicsDevice graphicsDevice_, Size size,
            SurfaceFormat _surfaceFormat, DepthFormat _depthFormat,
            AntialiasingType antialiasingType = AntialiasingType.NoAntialiasing, bool mipMap = false)
            : base(graphicsDevice_)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = _surfaceFormat;
            DepthFormat = _depthFormat;
            Antialiasing = antialiasingType;
            MipMap = mipMap;

            Create();
            GraphicsDevice.DeviceReset += OnScreenSizeChanged;
        } // RenderTarget

        public RenderTarget(
            GraphicsDevice graphicsDevice_,
            Size size,
            SurfaceFormat _surfaceFormat = SurfaceFormat.Color,
            bool _hasDepthBuffer = true,
            AntialiasingType antialiasingType = AntialiasingType.NoAntialiasing,
            bool mipMap = false)
            : base(graphicsDevice_)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = _surfaceFormat;
            DepthFormat = _hasDepthBuffer ? DepthFormat.Depth24 : DepthFormat.None;
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
                renderTarget = new RenderTarget2D(GraphicsDevice, Width, Height, MipMap, SurfaceFormat,
                    DepthFormat, CalculateMultiSampleQuality(Antialiasing), RenderTargetUsage.PlatformContents);
                alreadyResolved = true;
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
            renderTarget.Dispose();
        } // DisposeManagedResources



        private void OnScreenSizeChanged(object sender, EventArgs e)
        {
            // Just do it. Sometimes the render targets are not automatically recreated. This seems to happen with floating point surface format.
            //if (Size == Size.FullScreen || Size == Size.HalfScreen || Size == Size.QuarterScreen ||
            //    Size == Size.SplitFullScreen || Size == Size.SplitHalfScreen || Size == Size.SplitQuarterScreen)
            {
                // Render Targets don't use content managers.
                renderTarget.Dispose();
                OnDeviceReset(GraphicsDevice);
            }
        } // OnScreenSizeChanged



        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            Create();
            // Redo the bindings
            if (renderTargetBinding.HasValue)
            {
                for (int i = 0; i < renderTargetBinding.Value.InternalBinding.Length; i++)
                {
                    renderTargetBinding.Value.InternalBinding[i] =
                        new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTargetBinding.Value.RenderTargets[i].renderTarget);
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
                    return Engine.Instance.MultiSampleQuality;
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
                throw new InvalidOperationException("Render Target: unable to set render target. Another render target is still set. If you want to set multiple render targets use the static method called EnableRenderTargets.");
            GraphicsDevice.SetRenderTarget(renderTarget);
            currentRenderTarget[0] = this;
            alreadyResolved = false;
        } // EnableRenderTarget

        public static void EnableRenderTargets(GraphicsDevice graphicsDevice_, RenderTargetBinding renderTargetBinding)
        {
            if (currentRenderTarget[0] != null)
                throw new InvalidOperationException("Render Target: unable to set render target. Another render target is still set.");
            for (int i = 0; i < renderTargetBinding.RenderTargets.Length; i++)
            {
                currentRenderTarget[i] = renderTargetBinding.RenderTargets[i];
                renderTargetBinding.RenderTargets[i].alreadyResolved = false;
            }
            try
            {
                graphicsDevice_.SetRenderTargets(renderTargetBinding.InternalBinding);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Render Target. Unable to bind the render targets.", e);
            }
        } // EnableRenderTargets



        public void Clear(Color clearColor)
        {
            if (currentRenderTarget[0] != this)
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            if (DepthFormat == DepthFormat.None)
                GraphicsDevice.Clear(clearColor);
            else if (DepthFormat == DepthFormat.Depth24Stencil8)
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1.0f, 0);
            else
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, clearColor, 1.0f, 0);
        } // Clear

        public static void ClearCurrentRenderTargets(Color clearColor)
        {
            if (currentRenderTarget[0] == null)
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
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
                throw new InvalidOperationException("Render Target: There are multiple render targets enabled. Use RenderTarget.BackToBackBuffer instead.");

            alreadyResolved = true;
            currentRenderTarget[0] = null;
            GraphicsDevice.SetRenderTarget(null);
        } // DisableRenderTarget

        public static void DisableCurrentRenderTargets(GraphicsDevice graphicsDevice_)
        {
            for (int i = 0; i < 4; i++)
            {
                if (currentRenderTarget[i] != null)
                    currentRenderTarget[i].alreadyResolved = true;
                currentRenderTarget[i] = null;
            }
            graphicsDevice_.SetRenderTarget(null);
        } // DisableCurrentRenderTargets



        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2)
        {
            RenderTargetBinding renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2.renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2 }
            };
            renderTarget1.renderTargetBinding = renderTargetsBinding;
            renderTarget2.renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets

        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2, RenderTarget renderTarget3)
        {
            RenderTargetBinding renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget3.renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2, renderTarget3 }
            };
            renderTarget1.renderTargetBinding = renderTargetsBinding;
            renderTarget2.renderTargetBinding = renderTargetsBinding;
            renderTarget3.renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets

        public static RenderTargetBinding BindRenderTargets(RenderTarget renderTarget1, RenderTarget renderTarget2, RenderTarget renderTarget3, RenderTarget renderTarget4)
        {
            RenderTargetBinding renderTargetsBinding = new RenderTargetBinding
            {
                InternalBinding = new[]
                {
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget1.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget2.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget3.renderTarget),
                    new Microsoft.Xna.Framework.Graphics.RenderTargetBinding(renderTarget4.renderTarget),
                },
                RenderTargets = new[] { renderTarget1, renderTarget2, renderTarget3, renderTarget4 }
            };
            renderTarget1.renderTargetBinding = renderTargetsBinding;
            renderTarget2.renderTargetBinding = renderTargetsBinding;
            renderTarget3.renderTargetBinding = renderTargetsBinding;
            renderTarget4.renderTargetBinding = renderTargetsBinding;
            return renderTargetsBinding;
        } // BindRenderTargets



        // A pool of all render targets.
        private static readonly List<RenderTarget> renderTargets = new List<RenderTarget>(0);

        public static RenderTarget Fetch(GraphicsDevice graphicsDevice_, Size size, SurfaceFormat surfaceFormat, DepthFormat depthFormat, AntialiasingType antialiasingType, bool mipMap = false)
        {
            RenderTarget renderTarget;
            for (int i = 0; i < renderTargets.Count; i++)
            {
                renderTarget = renderTargets[i];
                if (renderTarget.Size == size && renderTarget.SurfaceFormat == surfaceFormat &&
                    renderTarget.DepthFormat == depthFormat && renderTarget.Antialiasing == antialiasingType && renderTarget.MipMap == mipMap && !renderTarget.looked)
                {
                    renderTarget.looked = true;
                    return renderTarget;
                }
            }
            // If there is not one unlook or present we create one.
            renderTarget = new RenderTarget(graphicsDevice_, size, surfaceFormat, depthFormat, antialiasingType, mipMap);
            renderTargets.Add(renderTarget);
            renderTarget.looked = true;
            return renderTarget;
        } // Fetch

        public static void Release(RenderTarget rendertarget)
        {
            if (rendertarget == null)
                return;
            for (int i = 0; i < renderTargets.Count; i++)
            {
                if (rendertarget == renderTargets[i])
                {
                    rendertarget.looked = false;
                    return;
                }
            }
            // If not do nothing.
            //throw new ArgumentException("Render Target: Cannot release render target. The render target is not present in the pool.");
        } // Release

        public static void ClearRenderTargetPool()
        {
            for (int i = 0; i < renderTargets.Count; i++)
                renderTargets[i].Dispose();
            renderTargets.Clear();
        } // ClearRenderTargetPool



        // A pool of all multiple render targets.
        private static readonly List<RenderTargetBinding> multipleRenderTargets = new List<RenderTargetBinding>(0);

        public static RenderTargetBinding Fetch(GraphicsDevice graphicsDevice_, Size size, SurfaceFormat surfaceFormat1, DepthFormat depthFormat, SurfaceFormat surfaceFormat2)
        {
            RenderTargetBinding renderTargetBinding;
            for (int i = 0; i < multipleRenderTargets.Count; i++)
            {
                renderTargetBinding = multipleRenderTargets[i];
                // If is a multiple render target of three render targets.
                if (renderTargetBinding.RenderTargets.Length == 2)
                {
                    if (renderTargetBinding.RenderTargets[0].Size == size && renderTargetBinding.RenderTargets[0].SurfaceFormat == surfaceFormat1 &&
                        renderTargetBinding.RenderTargets[0].DepthFormat == depthFormat &&
                        renderTargetBinding.RenderTargets[1].SurfaceFormat == surfaceFormat2 &&
                        !renderTargetBinding.RenderTargets[0].looked)
                    {
                        renderTargetBinding.RenderTargets[0].looked = true;
                        return renderTargetBinding;
                    }
                }
            }
            // If there is not one unlook or present we create one.
            RenderTarget renderTarget1 = new RenderTarget(graphicsDevice_, size, surfaceFormat1, depthFormat, AntialiasingType.NoAntialiasing);
            RenderTarget renderTarget2 = new RenderTarget(graphicsDevice_, size, surfaceFormat2, false, AntialiasingType.NoAntialiasing);
            renderTargetBinding = BindRenderTargets(renderTarget1, renderTarget2);
            multipleRenderTargets.Add(renderTargetBinding);
            renderTargetBinding.RenderTargets[0].looked = true;
            return renderTargetBinding;
        } // Fetch

        public static RenderTargetBinding Fetch(GraphicsDevice graphicsDevice_, Size size, SurfaceFormat surfaceFormat1, DepthFormat depthFormat, SurfaceFormat surfaceFormat2, SurfaceFormat surfaceFormat3)
        {
            RenderTargetBinding renderTargetBinding;
            for (int i = 0; i < multipleRenderTargets.Count; i++)
            {
                renderTargetBinding = multipleRenderTargets[i];
                // If is a multiple render target of three render targets.
                if (renderTargetBinding.RenderTargets.Length == 3)
                {
                    if (renderTargetBinding.RenderTargets[0].Size == size && renderTargetBinding.RenderTargets[0].SurfaceFormat == surfaceFormat1 &&
                        renderTargetBinding.RenderTargets[0].DepthFormat == depthFormat &&
                        renderTargetBinding.RenderTargets[1].SurfaceFormat == surfaceFormat2 &&
                        renderTargetBinding.RenderTargets[2].SurfaceFormat == surfaceFormat3 &&
                        !renderTargetBinding.RenderTargets[0].looked)
                    {
                        renderTargetBinding.RenderTargets[0].looked = true;
                        return renderTargetBinding;
                    }
                }
            }
            // If there is not one unlook or present we create one.
            RenderTarget renderTarget1 = new RenderTarget(graphicsDevice_, size, surfaceFormat1, depthFormat, AntialiasingType.NoAntialiasing);
            RenderTarget renderTarget2 = new RenderTarget(graphicsDevice_, size, surfaceFormat2, false, AntialiasingType.NoAntialiasing);
            RenderTarget renderTarget3 = new RenderTarget(graphicsDevice_, size, surfaceFormat3, false, AntialiasingType.NoAntialiasing);
            renderTargetBinding = BindRenderTargets(renderTarget1, renderTarget2, renderTarget3);
            multipleRenderTargets.Add(renderTargetBinding);
            renderTargetBinding.RenderTargets[0].looked = true;
            return renderTargetBinding;
        } // Fetch

        public static void Release(RenderTargetBinding renderTargetBinding)
        {
            for (int i = 0; i < multipleRenderTargets.Count; i++)
            {
                if (renderTargetBinding == multipleRenderTargets[i])
                {
                    renderTargetBinding.RenderTargets[0].looked = false;
                    return;
                }
            }
            // If not do nothing.
            //throw new ArgumentException("Render Target: Cannot release multiple render target. The multiple render target is not present in the pool.");
        } // Release

        public static void ClearMultpleRenderTargetPool()
        {
            for (int i = 0; i < multipleRenderTargets.Count; i++)
            {
                for (int j = 0; j < multipleRenderTargets[i].RenderTargets.Length; j++)
                    multipleRenderTargets[i].RenderTargets[j].Dispose();
            }
            multipleRenderTargets.Clear();
        } // ClearMultpleRenderTargetPool


    } // RenderTarget
} // CasaEngine.Asset
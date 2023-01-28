
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

using System;


using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using XNAFinalEngine.Helpers;
using CasaEngine.Game;
using CasaEngine.CoreSystems;
using Size = XNAFinalEngine.Helpers.Size;

namespace CasaEngine.Asset
{
    /// <summary>
    /// Allows to render onto textures, this kind of textures are called Render Targets.
    /// </summary>
    public sealed class RenderTarget : Texture
    {


        /// <summary>
        /// This structure is used to set multiple render targets without generating garbage in the process.
        /// Using directly something like this GraphicsDevice.SetRenderTargets(diffuseRt, normalRt, depthRt) will generate garbage.
        /// </summary>
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



        /// <summary>
        /// Antialiasing Type.
        /// </summary>
        public enum AntialiasingType
        {
            /// <summary>
            /// Back Buffer value.
            /// </summary>
            System,
            /// <summary>
            /// No antialiasing.
            /// </summary>
            NoAntialiasing,
            /// <summary>
            /// 2X antialiasing.
            /// </summary>
            TwoSamples,
            /// <summary>
            /// 4X antialiasing.
            /// </summary>
            FourSamples,
            /// <summary>
            /// 8X antialiasing.
            /// </summary>
            EightSamples,
            /// <summary>
            /// 16X antialiasing.
            /// </summary>
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

        /// <summary>
        /// Render Target Binding.
        /// </summary>
        private RenderTargetBinding? renderTargetBinding;



        /// <summary>
        /// Return the render target texture. In XNA 4.0 the render target it's a texture.
        /// </summary>
        public override Texture2D Resource
        {
            get
            {
                if (alreadyResolved)
                    return renderTarget;
                throw new InvalidOperationException("Render Target: Unable to return render target. Render target not resolved.");
            }
        } // Resource

        /// <summary>
        /// Surface Format.
        /// </summary>
        public SurfaceFormat SurfaceFormat { get; private set; }

        /// <summary>
        /// Depth Format.
        /// </summary>
        public DepthFormat DepthFormat { get; private set; }

        /// <summary>
        /// Multi Sample Quality.
        /// </summary>
        public AntialiasingType Antialiasing { get; private set; }

        /// <summary>
        /// True if a full mipmap chain will be generated.
        /// </summary>
        public bool MipMap { get; private set; }

        /// <summary>
        /// Currently active render targets.
        /// </summary>
        public static RenderTarget[] CurrentRenderTarget { get { return currentRenderTarget; } }



        /// <summary>
        /// Creates a render target for render to textures. Use size type constructor for screen relative sizes.
        /// </summary>
        /// <param name="size">Render target size</param>
        /// <param name="_surfaceFormat">Surface format</param>
        /// <param name="_depthFormat">Depth Format</param>
        /// <param name="antialiasingType">Multi sampling type: System value or no antialiasing.</param>
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

        /// <summary>
        /// Creates a render target for render to textures. Use size type constructor for screen relative sizes.
        /// </summary>
        /// <param name="size">Render target size</param>
        /// <param name="_surfaceFormat">Surface format</param>
        /// <param name="_hasDepthBuffer">Has depth buffer?</param>
        /// <param name="antialiasingType">Multi sampling type: System value or no antialiasing.</param>
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



        /// <summary>
        /// Creates render target.
        /// </summary>
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



        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            base.DisposeManagedResources();
            GraphicsDevice.DeviceReset -= OnScreenSizeChanged;
            renderTarget.Dispose();
        } // DisposeManagedResources



        /// <summary>
        /// In older versions of XNA, when a Device was lost you had to manually recreate some resources.
        /// Now there is no need to do it, however some render targets are bound to the window size, so these render targets will be recreated with the correct size.
        /// </summary>
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



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
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



        /// <summary>
        /// Calculate multiSample quality.
        /// </summary>
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



        /// <summary>
        /// Set render target for render.
        /// </summary>
        public void EnableRenderTarget()
        {
            if (currentRenderTarget[0] != null)
                throw new InvalidOperationException("Render Target: unable to set render target. Another render target is still set. If you want to set multiple render targets use the static method called EnableRenderTargets.");
            GraphicsDevice.SetRenderTarget(renderTarget);
            currentRenderTarget[0] = this;
            alreadyResolved = false;
        } // EnableRenderTarget

        /// <summary>
        /// Enable multiple render targets.
        /// </summary>
        /// <param name="renderTargetBinding">
        /// This structure is used to set multiple render targets without generating garbage in the process.
        /// You can create it using the BindRenderTargets method.
        /// </param>
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



        /// <summary>
        /// Clear render target.
        /// This method will only work if the render target was set before with SetRenderTarget.
        /// </summary>
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

        /// <summary>
        /// Clear render target.
        /// This is the same as calling Clear from the first render target.
        /// </summary>
        public static void ClearCurrentRenderTargets(Color clearColor)
        {
            if (currentRenderTarget[0] == null)
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            currentRenderTarget[0].Clear(clearColor);
        } // Clear



        /// <summary>
        /// Resolve render target.
        /// This method will only work if the render target was set before with SetRenderTarget.
        /// </summary>
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

        /// <summary>
        /// Back to back buffer (frame buffer).
        /// </summary>
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



        /// <summary>
        /// Bind render targets so that they could be set together without generate garbage in the process.
        /// </summary>
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

        /// <summary>
        /// Bind render targets so that they could be set together without generate garbage in the process.
        /// </summary>
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

        /// <summary>
        /// Bind render targets so that they could be set together without generate garbage in the process.
        /// </summary>
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

        /// <summary>
        /// There is a pool of render targets to avoid wasting unnecessary graphic memory.
        /// The idea is that a render target has also a flag that tell us if the content is still need or not.
        /// So, when a shader needs a render target it search in the pool for an unused render target with the right characteristics (size, surface format, etc.)
        /// The problem if someone has to turn the flag false when the render target’s content is unnecessary and this could be somehow ugly. 
        /// But the graphic pipeline performance is critical, it’s not an area for the user and its complexity was diminished thanks to the new code’s restructuring.
        /// The pool should be used in the filters, shadow maps and similar shaders. Not everything.
        /// Use the Release method to return a render target to the pool.
        /// </summary>
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

        /// <summary>
        /// Release the render target.
        /// </summary>
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

        /// <summary>
        /// Fetch a multiple render target.
        /// </summary>
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

        /// <summary>
        /// Fetch a multiple render target.
        /// </summary>
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

        /// <summary>
        /// Release the multiple render target.
        /// </summary>
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
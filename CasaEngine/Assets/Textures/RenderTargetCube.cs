
/*
Copyright (c) 2008-2013, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
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
using CasaEngine.Game;


namespace CasaEngine.Asset
{
    /// <summary>
    /// Allows to render onto cube textures, this kind of textures are called Cube Render Targets. 
    /// </summary>
    /// <remarks>
    /// The engine design initially excludes cube render targets and because they are used in very specific scenarios
    /// I decided to ignore some checking like controlling that a 2D render target is not active when the cube render target set and vice versa.
    /// </remarks>
    public sealed class RenderTargetCube : TextureCube
    {


        // XNA Render target.
        private Microsoft.Xna.Framework.Graphics.RenderTargetCube renderTarget;

        // Make sure we don't call xnaTexture before resolving for the first time!
        private bool alreadyResolved;

        // Indicates if this render target is currently used and if its information has to be preserved.
        private bool looked;

        // Remember the last render targets we set. We can enable up to four render targets at once.
        private static RenderTargetCube currentRenderTarget;



        /// <summary>
        /// Return the render target texture. In XNA 4.0 the render target it's a texture.
        /// </summary>
        public override Microsoft.Xna.Framework.Graphics.TextureCube Resource
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
        public RenderTarget.AntialiasingType Antialiasing { get; private set; }

        /// <summary>
        /// True if a full mipmap chain will be generated.
        /// </summary>
        public bool MipMap { get; private set; }

        /// <summary>
        /// Currently active render targets.
        /// </summary>
        public static RenderTargetCube CurrentRenderTarget { get { return currentRenderTarget; } }



        /// <summary>
        /// Creates a render target for render to textures. Use size type constructor for screen relative sizes.
        /// </summary>
        /// <param name="size">Render target size</param>
        /// <param name="_surfaceFormat">Surface format</param>
        /// <param name="_depthFormat">Depth Format</param>
        /// <param name="antialiasingType">Multi sampling type: System value or no antialiasing.</param>
        public RenderTargetCube(int size, SurfaceFormat _surfaceFormat, DepthFormat _depthFormat, RenderTarget.AntialiasingType antialiasingType = RenderTarget.AntialiasingType.NoAntialiasing, bool mipMap = false)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = _surfaceFormat;
            DepthFormat = _depthFormat;
            Antialiasing = antialiasingType;
            MipMap = mipMap;

            Create();
        } // RenderTarget

        /// <summary>
        /// Creates a render target for render to textures. Use size type constructor for screen relative sizes.
        /// </summary>
        /// <param name="size">Render target size</param>
        /// <param name="_surfaceFormat">Surface format</param>
        /// <param name="_hasDepthBuffer">Has depth buffer?</param>
        /// <param name="antialiasingType">Multi sampling type: System value or no antialiasing.</param>
        public RenderTargetCube(int size, SurfaceFormat _surfaceFormat = SurfaceFormat.Color, bool _hasDepthBuffer = true, RenderTarget.AntialiasingType antialiasingType = RenderTarget.AntialiasingType.NoAntialiasing, bool mipMap = false)
        {
            Name = "Render Target";
            Size = size;

            SurfaceFormat = _surfaceFormat;
            DepthFormat = _hasDepthBuffer ? DepthFormat.Depth24 : DepthFormat.None;
            Antialiasing = antialiasingType;
            MipMap = mipMap;

            Create();
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
                renderTarget = new Microsoft.Xna.Framework.Graphics.RenderTargetCube(Engine.Instance.Game.GraphicsDevice, Size, MipMap, SurfaceFormat, DepthFormat, RenderTarget.CalculateMultiSampleQuality(Antialiasing), RenderTargetUsage.PlatformContents);
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
            renderTarget.Dispose();
        } // DisposeManagedResources



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            Create();
        } // RecreateResource



        /// <summary>
        /// Set render target for render.
        /// </summary>
        public void EnableRenderTarget(CubeMapFace cubeMapFace)
        {
            if (currentRenderTarget != null)
                throw new InvalidOperationException("Render Target Cube: unable to set render target. Another render target is still set. If you want to set multiple render targets use the static method called EnableRenderTargets.");
            Engine.Instance.Game.GraphicsDevice.SetRenderTarget(renderTarget, cubeMapFace);
            currentRenderTarget = this;
            alreadyResolved = false;
        } // EnableRenderTarget



        /// <summary>
        /// Clear render target.
        /// This method will only work if the render target was set before with SetRenderTarget.
        /// </summary>
        public void Clear(Color clearColor)
        {
            if (currentRenderTarget != this)
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            if (DepthFormat == DepthFormat.None)
                Engine.Instance.Game.GraphicsDevice.Clear(clearColor);
            else if (DepthFormat == DepthFormat.Depth24Stencil8)
                Engine.Instance.Game.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1.0f, 0);
            else
                Engine.Instance.Game.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, clearColor, 1.0f, 0);
        } // Clear

        /// <summary>
        /// Clear render target.
        /// This is the same as calling Clear from the first render target.
        /// </summary>
        public static void ClearCurrentRenderTargets(Color clearColor)
        {
            if (currentRenderTarget == null)
                throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
            currentRenderTarget.Clear(clearColor);
        } // Clear



        /// <summary>
        /// Resolve render target.
        /// This method will only work if the render target was set before with SetRenderTarget.
        /// </summary>
        public void DisableRenderTarget()
        {
            // Make sure this render target is currently set!
            if (currentRenderTarget != this)
            {
                throw new InvalidOperationException("Render Target: Cannot call disable to a render target without first setting it.");
            }
            alreadyResolved = true;
            currentRenderTarget = null;
            Engine.Instance.Game.GraphicsDevice.SetRenderTarget(null);
        } // DisableRenderTarget

        /// <summary>
        /// Back to back buffer (frame buffer).
        /// </summary>
        public static void DisableCurrentRenderTargets()
        {
            if (currentRenderTarget != null)
                currentRenderTarget.alreadyResolved = true;
            currentRenderTarget = null;
            Engine.Instance.Game.GraphicsDevice.SetRenderTarget(null);
        } // DisableCurrentRenderTargets



        // A pool of all render targets.
        private static readonly List<RenderTargetCube> renderTargets = new List<RenderTargetCube>(0);

        /// <summary>
        /// There is a pool of render targets to avoid wasting unnecessary graphic memory.
        /// The idea is that a render target has also a flag that tell us if the content is still need or not.
        /// So, when a shader needs a render target it search in the pool for an unused render target with the right characteristics (size, surface format, etc.)
        /// The problem if someone has to turn the flag false when the render target’s content is unnecessary and this could be somehow ugly. 
        /// But the graphic pipeline performance is critical, it’s not an area for the user and its complexity was diminished thanks to the new code’s restructuring.
        /// The pool should be used in the filters, shadow maps and similar shaders. Not everything.
        /// Use the Release method to return a render target to the pool.
        /// </summary>
        public static RenderTargetCube Fetch(int size, SurfaceFormat surfaceFormat, DepthFormat depthFormat, RenderTarget.AntialiasingType antialiasingType, bool mipMap = false)
        {
            RenderTargetCube renderTarget;
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
            renderTarget = new RenderTargetCube(size, surfaceFormat, depthFormat, antialiasingType, mipMap);
            renderTargets.Add(renderTarget);
            renderTarget.looked = true;
            return renderTarget;
        } // Fetch

        /// <summary>
        /// Release the render target.
        /// </summary>
        public static void Release(RenderTargetCube rendertarget)
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


    } // RenderTargetCube
} // CasaEngine.Asset
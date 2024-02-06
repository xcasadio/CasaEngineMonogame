
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

using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Textures;

public sealed class RenderTargetCube : TextureCube
{
    // XNA Render target.
    private Microsoft.Xna.Framework.Graphics.RenderTargetCube _renderTarget;

    // Make sure we don't call xnaTexture before resolving for the first time!
    private bool _alreadyResolved;

    // Indicates if this render target is currently used and if its information has to be preserved.
    private bool _looked;

    // Remember the last render targets we set. We can enable up to four render targets at once.
    private static RenderTargetCube? _currentRenderTarget;

    public override Microsoft.Xna.Framework.Graphics.TextureCube Resource
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

    public RenderTarget.AntiAliasingType AntiAliasing { get; private set; }

    public bool MipMap { get; private set; }

    public static RenderTargetCube CurrentRenderTarget => _currentRenderTarget;

    public RenderTargetCube(GraphicsDevice graphicsDevice, int size, SurfaceFormat surfaceFormat, DepthFormat depthFormat, RenderTarget.AntiAliasingType antiAliasingType = RenderTarget.AntiAliasingType.NoAntialiasing, bool mipMap = false)
    {
        //Name = "Render Target";
        Size = size;

        SurfaceFormat = surfaceFormat;
        DepthFormat = depthFormat;
        AntiAliasing = antiAliasingType;
        MipMap = mipMap;

        Create(graphicsDevice);
    }

    public RenderTargetCube(GraphicsDevice graphicsDevice, int size, SurfaceFormat surfaceFormat = SurfaceFormat.Color, bool hasDepthBuffer = true, RenderTarget.AntiAliasingType antiAliasingType = RenderTarget.AntiAliasingType.NoAntialiasing, bool mipMap = false)
    {
        //Name = "Render Target";
        Size = size;

        SurfaceFormat = surfaceFormat;
        DepthFormat = hasDepthBuffer ? DepthFormat.Depth24 : DepthFormat.None;
        AntiAliasing = antiAliasingType;
        MipMap = mipMap;

        Create(graphicsDevice);
    }

    private void Create(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        try
        {
            // Create render target of specified size.
            // On Xbox 360, the render target will discard contents. On PC, the render target will discard if multisampling is enabled, and preserve the contents if not.
            // I use RenderTargetUsage.PlatformContents to be little more performance friendly with PC.
            // But I assume that the system works in DiscardContents mode so that an XBOX 360 implementation works.
            // What I lose, mostly nothing, because I made my own ZBuffer texture and the stencil buffer is deleted no matter what I do.
            _renderTarget = new Microsoft.Xna.Framework.Graphics.RenderTargetCube(graphicsDevice, Size, MipMap, SurfaceFormat, DepthFormat, RenderTarget.CalculateMultiSampleQuality(AntiAliasing), RenderTargetUsage.PlatformContents);
            _alreadyResolved = true;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Render target creation failed", e);
        }
    }

    protected override void DisposeManagedResources()
    {
        base.DisposeManagedResources();
        _renderTarget.Dispose();
    }

    public override void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        Create(device);
    }

    public void EnableRenderTarget(CubeMapFace cubeMapFace)
    {
        if (_currentRenderTarget != null)
        {
            throw new InvalidOperationException("Render Target Cube: unable to set render target. Another render target is still set. If you want to set multiple render targets use the static method called EnableRenderTargets.");
        }

        _graphicsDevice.SetRenderTarget(_renderTarget, cubeMapFace);
        _currentRenderTarget = this;
        _alreadyResolved = false;
    }

    public void Clear(Color clearColor)
    {
        if (_currentRenderTarget != this)
        {
            throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
        }

        if (DepthFormat == DepthFormat.None)
        {
            _graphicsDevice.Clear(clearColor);
        }
        else if (DepthFormat == DepthFormat.Depth24Stencil8)
        {
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1.0f, 0);
        }
        else
        {
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, clearColor, 1.0f, 0);
        }
    }

    public static void ClearCurrentRenderTargets(Color clearColor)
    {
        if (_currentRenderTarget == null)
        {
            throw new InvalidOperationException("Render Target: You can't clear a render target without first setting it");
        }

        _currentRenderTarget.Clear(clearColor);
    }

    public void DisableRenderTarget()
    {
        // Make sure this render target is currently set!
        if (_currentRenderTarget != this)
        {
            throw new InvalidOperationException("Render Target: Cannot call disable to a render target without first setting it.");
        }
        _alreadyResolved = true;
        _currentRenderTarget = null;
        _graphicsDevice.SetRenderTarget(null);
    } // DisableRenderTarget

    public void DisableCurrentRenderTargets()
    {
        if (_currentRenderTarget != null)
        {
            _currentRenderTarget._alreadyResolved = true;
        }

        _currentRenderTarget = null;
        _graphicsDevice.SetRenderTarget(null);
    } // DisableCurrentRenderTargets

    // A pool of all render targets.
    private static readonly List<RenderTargetCube> RenderTargets = new(0);
    private GraphicsDevice _graphicsDevice;

    public static RenderTargetCube Fetch(GraphicsDevice graphicsDevice, int size, SurfaceFormat surfaceFormat, DepthFormat depthFormat, RenderTarget.AntiAliasingType antiAliasingType, bool mipMap = false)
    {
        RenderTargetCube renderTarget;
        for (var i = 0; i < RenderTargets.Count; i++)
        {
            renderTarget = RenderTargets[i];
            if (renderTarget.Size == size && renderTarget.SurfaceFormat == surfaceFormat &&
                renderTarget.DepthFormat == depthFormat && renderTarget.AntiAliasing == antiAliasingType && renderTarget.MipMap == mipMap && !renderTarget._looked)
            {
                renderTarget._looked = true;
                return renderTarget;
            }
        }
        // If there is not one unlook or present we create one.
        renderTarget = new RenderTargetCube(graphicsDevice, size, surfaceFormat, depthFormat, antiAliasingType, mipMap);
        RenderTargets.Add(renderTarget);
        renderTarget._looked = true;
        return renderTarget;
    } // Fetch

    public static void Release(RenderTargetCube rendertarget)
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
    }
}

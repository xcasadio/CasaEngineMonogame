
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


using System.Xml;
using CasaEngine.Core.Design;
using Microsoft.Xna.Framework.Graphics;
using Screen = CasaEngine.Framework.UserInterface.Screen;
using Size = CasaEngine.Core.Helpers.Size;


namespace CasaEngine.Framework.Assets.Textures;

public class Texture : Asset
{


    protected Texture2D XnaTexture;

    // Default value.
    private SamplerState _preferedSamplerState = SamplerState.AnisotropicWrap;

    // Simple and small textures filled with a constant color.
    private static Texture _blackTexture, _greyTexture, _whiteTexture;



    public GraphicsDevice GraphicsDevice { get; private set; }


    public virtual Texture2D Resource
    {
        get =>
            // Textures and render targets have a different treatment because textures could be set,
            // because both are persistent shader parameters, and because they could be created without using content managers.
            // For that reason the nullified resources could be accessed.
            //if (xnaTexture != null && xnaTexture.IsDisposed)
            //xnaTexture = null;
            XnaTexture;
        // This is only allowed for videos. 
        // Doing something to avoid this “set” is unnecessary and probably will make more complex some classes just for this special case. 
        // Besides, an internal statement elegantly prevents a bad use of this set.
        // Just don’t dispose this texture because the resource is managed by the video.
        internal set
        {
            XnaTexture = value;
            Size = value == null ?
                new Size(0, 0, new Screen(GraphicsDevice)) : new Size(XnaTexture.Width, XnaTexture.Height, new Screen(GraphicsDevice));
        }
    } // Resource



    public virtual SamplerState PreferredSamplerState
    {
        get => _preferedSamplerState;
        set => _preferedSamplerState = value;
    } // PreferredSamplerState



    public int Width => Size.Width;

    public int Height => Size.Height;

    public Rectangle TextureRectangle => new(0, 0, Width, Height);

    public Size Size { get; protected set; }




    internal Texture(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
        Name = "Empty Texture";
    } // Texture

    public Texture(Texture2D xnaTexture)
    {
        GraphicsDevice = xnaTexture.GraphicsDevice;
        Name = "Texture";
        XnaTexture = xnaTexture;
        Size = new Size(xnaTexture.Width, xnaTexture.Height, new Screen(GraphicsDevice));
    } // Texture

    public Texture(GraphicsDevice graphicsDevice, string filename)
    {
        GraphicsDevice = graphicsDevice;
        Name = filename;
        Filename = EngineComponents.AssetContentManager.RootDirectory + Path.DirectorySeparatorChar + filename;
        if (File.Exists(Filename) == false)
        {
            throw new ArgumentException("Failed to load texture: File " + Filename + " does not exists!", nameof(filename));
        }
        try
        {
            XnaTexture = EngineComponents.AssetContentManager.Load<Texture2D>(Filename, GraphicsDevice);
            Size = new Size(XnaTexture.Width, XnaTexture.Height, new Screen(GraphicsDevice));
            Resource.Name = filename;
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to load texture: " + filename, e);
        }
    } // Texture




    protected override void DisposeManagedResources()
    {
        base.DisposeManagedResources();
        if (XnaTexture != null && !XnaTexture.IsDisposed)
        {
            Resource.Dispose();
        }
    } // DisposeManagedResources



    internal override void OnDeviceReset(GraphicsDevice device)
    {
        if (Resource == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(Filename))
        {
            XnaTexture = new Texture2D(device, Size.Width, Size.Height);
        }
        else if (XnaTexture.IsDisposed)
        {
            XnaTexture = EngineComponents.AssetContentManager.Load<Texture2D>(Filename, device);
        }

        GraphicsDevice = device;
    } // RecreateResource



    public override void Load(BinaryReader br, SaveOption option)
    {

    }

    public override void Load(XmlElement el, SaveOption option)
    {

    }



} // Texture
  // CasaEngine.Asset
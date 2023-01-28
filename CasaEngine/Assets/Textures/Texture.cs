
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


using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using XNAFinalEngine.Helpers;
using CasaEngine.Game;
using System.Xml;
using CasaEngineCommon.Design;
using CasaEngine.CoreSystems;

using Screen = CasaEngine.CoreSystems.Screen;
using Size = XNAFinalEngine.Helpers.Size;

namespace CasaEngine.Asset
{

    /// <summary>
    /// Base class for textures.
    /// Important: Try to dispose only the textures created without the content manager.
    /// If you dispose a texture and then you try to load again using the same content managed an exception will be raised.
    /// In this cases use the Unload method from the Content Manager instead.
    /// </summary>
    public class Texture : Asset
    {


        /// <summary>
        /// XNA Texture.
        /// </summary>
        protected Texture2D xnaTexture;

        // Default value.
        private SamplerState preferedSamplerState = SamplerState.AnisotropicWrap;

        // Simple and small textures filled with a constant color.
        private static Texture blackTexture, greyTexture, whiteTexture;



        /// <summary>
        /// Gets the GraphicsDevice
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; private set; }


        /// <summary>
        /// XNA Texture.
        /// </summary>
        public virtual Texture2D Resource
        {
            get
            {
                // Textures and render targets have a different treatment because textures could be set,
                // because both are persistent shader parameters, and because they could be created without using content managers.
                // For that reason the nullified resources could be accessed.
                //if (xnaTexture != null && xnaTexture.IsDisposed)
                //xnaTexture = null;
                return xnaTexture;
            }
            // This is only allowed for videos. 
            // Doing something to avoid this “set” is unnecessary and probably will make more complex some classes just for this special case. 
            // Besides, an internal statement elegantly prevents a bad use of this set.
            // Just don’t dispose this texture because the resource is managed by the video.
            internal set
            {
                xnaTexture = value;
                Size = value == null ?
                    new Size(0, 0, new Screen(GraphicsDevice)) : new Size(xnaTexture.Width, xnaTexture.Height, new Screen(GraphicsDevice));
            }
        } // Resource



        /// <summary>
        /// Some shaders allow us to choose how to sample the texture data.
        /// </summary>
        public virtual SamplerState PreferredSamplerState
        {
            get { return preferedSamplerState; }
            set { preferedSamplerState = value; }
        } // PreferredSamplerState



        /// <summary>
        /// Texture's width.
        /// </summary>
        public int Width { get { return Size.Width; } }

        /// <summary>
        /// Texture's height.
        /// </summary>
        public int Height { get { return Size.Height; } }

        /// <summary>
        /// Rectangle that starts in 0, 0 and finish in the width and height of the texture. 
        /// </summary>
        public Rectangle TextureRectangle { get { return new Rectangle(0, 0, Width, Height); } }

        /// <summary>
        /// Size.
        /// This value store information about sizes relative to screen.
        /// </summary>
        public Size Size { get; protected set; }




        /// <summary>
        /// Empty texture. 
        /// </summary>
        internal Texture(GraphicsDevice graphicsDevice_)
        {
            GraphicsDevice = graphicsDevice_;
            Name = "Empty Texture";
        } // Texture

        /// <summary>
        /// Texture from XNA asset.
        /// </summary>
        public Texture(Texture2D xnaTexture)
        {
            GraphicsDevice = xnaTexture.GraphicsDevice;
            Name = "Texture";
            this.xnaTexture = xnaTexture;
            Size = new Size(xnaTexture.Width, xnaTexture.Height, new Screen(GraphicsDevice));
        } // Texture

        /// <summary>
        /// Load texture.
        /// </summary>
        /// <param name="filename">The filename must be relative and be a valid file in the textures directory.</param>
        public Texture(GraphicsDevice graphicsDevice_, string filename)
        {
            GraphicsDevice = graphicsDevice_;
            Name = filename;
            Filename = Engine.Instance.AssetContentManager.RootDirectory + Path.DirectorySeparatorChar + filename;
            if (File.Exists(Filename) == false)
            {
                throw new ArgumentException("Failed to load texture: File " + Filename + " does not exists!", "filename");
            }
            try
            {
                xnaTexture = Engine.Instance.AssetContentManager.Load<Texture2D>(Filename, GraphicsDevice);
                Size = new Size(xnaTexture.Width, xnaTexture.Height, new Screen(GraphicsDevice));
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




        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            base.DisposeManagedResources();
            if (xnaTexture != null && !xnaTexture.IsDisposed)
                Resource.Dispose();
        } // DisposeManagedResources



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            if (Resource == null)
                return;
            if (string.IsNullOrEmpty(Filename))
                xnaTexture = new Texture2D(device_, Size.Width, Size.Height);
            else if (xnaTexture.IsDisposed == true)
                xnaTexture = Engine.Instance.AssetContentManager.Load<Texture2D>(Filename, device_);

            GraphicsDevice = device_;
        } // RecreateResource



        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {

        }



    } // Texture
} // CasaEngine.Asset
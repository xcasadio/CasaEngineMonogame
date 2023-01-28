
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


using System.IO;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;

namespace CasaEngine.Asset
{

    /// <summary>
    /// LDR or HDR Cube Maps.
    /// HDR can be stored in the RGBM format.
    /// </summary>
    public class TextureCube : Asset
    {


        // XNA Texture.
        protected Microsoft.Xna.Framework.Graphics.TextureCube xnaTextureCube;

        // Simple and small textures filled with a constant color.
        private static TextureCube blackTexture, whiteTexture;



        /// <summary>
        /// XNA Texture.
        /// </summary>
        public virtual Microsoft.Xna.Framework.Graphics.TextureCube Resource { get { return xnaTextureCube; } }

        /// <summary>
        /// The size of this texture resource, in pixels.
        /// </summary>
        public int Size { get; protected set; }

        /// <summary>
        /// Is it in RGBM format?
        /// </summary>
        public bool IsRgbm { get; set; }

        /// <summary>
        /// RGBM Max Range.
        /// </summary>
        public float RgbmMaxRange { get; set; }

        /// <summary>
        ///  A list with all texture' filenames on the cube texture directory.
        /// </summary>
        /// <remarks>
        /// If there are memory limitations, this list could be eliminated for the release version.
        /// This is use only useful for the editor.
        /// </remarks>
        public static string[] Filenames { get; private set; }


        /// <summary>
        /// Returns a small cube texture filled with a black constant color.
        /// </summary>
        public static TextureCube BlackTexture
        {
            get
            {
                if (blackTexture == null)
                {
                    blackTexture = new TextureCube("BlackCube");
                }
                return blackTexture;
            }
        } // BlackTexture

        /// <summary>
        /// Returns a small cube texture filled with a white constant color.
        /// </summary>
        public static TextureCube WhiteTexture
        {
            get
            {
                if (whiteTexture == null)
                {
                    whiteTexture = new TextureCube("WhiteCube");
                }
                return whiteTexture;
            }
        } // WhiteTexture




        /// <summary>
        /// Create cube map from given filename.
        /// </summary>
        /// <param name="filename">Set filename, must be relative and be a valid file in the textures cube directory.</param>
        public TextureCube(string filename)
        {
            Name = filename;
            IsRgbm = false;
            RgbmMaxRange = 50;
            Filename = Engine.Instance.ProjectManager.ProjectPath + filename;
            if (File.Exists(Filename) == false)
            {
                throw new ArgumentException("Failed to load cube map: File " + Filename + " does not exists!");
            }
            try
            {
                xnaTextureCube = Engine.Instance.AssetContentManager.Load<Microsoft.Xna.Framework.Graphics.TextureCube>(Filename, Engine.Instance.Game.GraphicsDevice);
                Size = xnaTextureCube.Size;
                Resource.Name = filename;
            }
            catch (ObjectDisposedException)
            {
                throw new InvalidOperationException("Content Manager: Content manager disposed");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Failed to load cube map: " + filename, e);
            }
        } // TextureCube

        protected TextureCube() { }



        /// <summary>
        /// Useful when the XNA device is disposed.
        /// </summary>
        internal override void OnDeviceReset(GraphicsDevice device_)
        {
            xnaTextureCube = Engine.Instance.AssetContentManager.Load<Microsoft.Xna.Framework.Graphics.TextureCube>(Filename, device_);
        } // RecreateResource


    } // TextureCube
} // CasaEngine.Asset


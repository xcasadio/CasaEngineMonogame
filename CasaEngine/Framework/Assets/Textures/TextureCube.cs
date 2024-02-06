
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

using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Textures;

public class TextureCube : UObject, IAssetable
{
    // XNA Texture.
    protected Microsoft.Xna.Framework.Graphics.TextureCube XnaTextureCube;

    public virtual Microsoft.Xna.Framework.Graphics.TextureCube Resource => XnaTextureCube;

    public int Size { get; protected set; }

    public bool IsRgbm { get; set; }

    public float RgbmMaxRange { get; set; }

    public static string[] Filenames { get; private set; }

    public TextureCube(string filename, GraphicsDevice graphicsDevice, AssetContentManager assetContentManager)
    {
        //Name = filename;
        IsRgbm = false;
        RgbmMaxRange = 50;
        FileName = filename;

        if (File.Exists(FileName) == false)
        {
            throw new ArgumentException($"Failed to load cube map: File {FileName} does not exists!");
        }
        try
        {
            XnaTextureCube = assetContentManager.Load<Microsoft.Xna.Framework.Graphics.TextureCube>(Id);
            Size = XnaTextureCube.Size;
            Resource.Name = filename;
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to load cube map: {filename}", e);
        }
    }

    protected TextureCube() { }

    public void Dispose()
    {
        DisposeManagedResources();
    }

    protected virtual void DisposeManagedResources()
    {
        XnaTextureCube.Dispose();
    }

    public virtual void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        XnaTextureCube = assetContentManager.Load<Microsoft.Xna.Framework.Graphics.TextureCube>(Id);
    }
}

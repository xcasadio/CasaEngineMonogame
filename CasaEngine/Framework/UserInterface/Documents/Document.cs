
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

using System.Xml;
using System.Xml.Linq;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UserInterface.Documents;

public class Document : Asset
{
    public XDocument Resource { get; private set; }

    public Document(string filename)
    {
        Name = filename;
        FileName = EngineComponents.AssetContentManager.RootDirectory + Path.DirectorySeparatorChar + filename;
        //FileName = GameInfo.Instance.ProjectManager.ProjectPath + filename;
        if (File.Exists(FileName + ".skin") == false) //.xnb
        {
            throw new ArgumentException("Failed to load document: File " + FileName + " does not exists!", nameof(filename));
        }
        try
        {
            Resource = XDocument.Load(FileName + ".skin");
            //Resource = new XDocument();
            //Resource = GameInfo.Instance.AssetContentManager.Load<XDocument>(FileName, GameInfo.Instance.Game.GraphicsDevice);
            //Resource = AssetContentManager.CurrentContentManager.XnaContentManager.Load<XDocument>(FileName);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to load document: " + filename, e);
        }
    } // Document

    internal override void OnDeviceReset(GraphicsDevice device)
    {
        if (Resource == null)
        {
            Resource = EngineComponents.AssetContentManager.Load<XDocument>(FileName, device);
        }
    } // RecreateResource

    public override void Load(BinaryReader br, SaveOption option)
    {

    }

    public override void Load(XmlElement el, SaveOption option)
    {

    }

} // Document
// XNAFinalEngine.Assets

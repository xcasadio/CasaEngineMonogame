using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace CasaEngine.Asset
{
    class Texture2DLoader
        : IAssetLoader
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="device_"></param>
        /// <returns></returns>
        public object LoadAsset(string fileName_, GraphicsDevice device_)
        {
            return Texture2D.FromStream(device_, new FileStream(fileName_, FileMode.Open));
        }

    }
}

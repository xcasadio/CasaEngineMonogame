using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Asset
{
    /// <summary>
    /// 
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        object LoadAsset(string fileName_, GraphicsDevice device_);
    }
}

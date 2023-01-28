using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor.Assets
{
    public interface IAssetable
    {
        /// <summary>
        /// Gets
        /// </summary>
        List<string> AssetFileNames
        {
            get;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public class Asset2DRenamedEventArg
        : EventArgs
    {
        public Asset2DRenamedEventArg(string name_, string newName_)
        {
            AssetName = name_;
            NewAssetName = newName_;
        }

        public string AssetName;
        public string NewAssetName;
    }
}

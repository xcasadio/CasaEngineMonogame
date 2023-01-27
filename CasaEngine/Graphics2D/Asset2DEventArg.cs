using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public class Asset2DEventArg
        : EventArgs
    {
        public Asset2DEventArg(string name_)
        {
            AssetName = name_;
        }

        public string AssetName;
    }
}

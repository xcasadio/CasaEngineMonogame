using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor.Manipulation
{
    /// <summary>
    /// 
    /// </summary>
    public class AnchorLocationChangedEventArgs
        : EventArgs
    {
        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int OffsetX
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int OffsetY
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public AnchorLocationChangedEventArgs(int x, int y)
        {
            OffsetX = x;
            OffsetY = y;
        }
    }
}

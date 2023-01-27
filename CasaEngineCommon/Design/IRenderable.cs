using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngineCommon.Design
{
    /// <summary>
    /// 
    /// </summary>
    public interface IRenderable
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        void Draw(float elapsedTime_);

        /// <summary>
        /// Gets
        /// </summary>
        int Depth { get; }
        
        /// <summary>
        /// Sets
        /// </summary>
        float ZOrder { set; }

        /// <summary>
        /// Gets
        /// </summary>
        bool Visible { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine;
using CasaEngine.Design.Event;
using CasaEngine.Game;
using CasaEngineCommon.Pool;

namespace CasaEngine.Assets.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public class Animation2DFrameChangedEventArgs
        : EventArgs
    {
        Animation2D m_Animation2D;
        int m_OldFrame, m_NewFrame;

        public Animation2D Animation2D
        {
            get { return m_Animation2D; }
            internal set { m_Animation2D = value; }
        }

        public int OldFrame
        {
            get { return m_OldFrame; }
            internal set { m_OldFrame = value; }
        }

        public int NewFrame
        {
            get { return m_NewFrame; }
            internal set { m_NewFrame = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="anim2D_"></param>
        /// <param name="oldFrame_"></param>
        /// <param name="newFrame_"></param>
        public Animation2DFrameChangedEventArgs(Animation2D anim2D_, int oldFrame_, int newFrame_)
        {
            m_Animation2D = anim2D_;
            m_OldFrame = oldFrame_;
            m_NewFrame = newFrame_;
        }
    }
}

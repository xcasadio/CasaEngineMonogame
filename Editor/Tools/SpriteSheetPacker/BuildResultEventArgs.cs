
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
    /// <summary>
    /// 
    /// </summary>
    public class BuildResultEventArgs
        : EventArgs
    {

        string m_SpriteSheetFileName;
        string m_SpriteSheetMapFileName;
        bool m_DetectAnimation;



        /// <summary>
        /// 
        /// </summary>
        public string SpriteSheetFileName
        {
            get { return m_SpriteSheetFileName; }
        }

        /// <summary>
        /// 
        /// </summary>
        public string SpriteSheetMapFileName
        {
            get { return m_SpriteSheetMapFileName; }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DetectAnimations
        {
            get { return m_DetectAnimation; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="spriteSheetFileName_"></param>
        /// <param name="spriteSheetMapFileName_"></param>
        /// <param name="detectAnimation_"></param>
        public BuildResultEventArgs(string spriteSheetFileName_, string spriteSheetMapFileName_, bool detectAnimation_)
        {
            m_SpriteSheetFileName = spriteSheetFileName_;
            m_SpriteSheetMapFileName = spriteSheetMapFileName_;
            m_DetectAnimation = detectAnimation_;
        }



    }
}

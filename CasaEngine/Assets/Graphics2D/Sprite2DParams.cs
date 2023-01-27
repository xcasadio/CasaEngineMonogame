using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Math.Shape2D;
using CasaEngine;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class Sprite2DParams
        : ISaveLoad
    {
        #region Fields

        private Shape2DObject m_Shape2DObject;
        private Sprite2DParamsType m_Type;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public Shape2DObject Shape2DObject
        {
            get { return m_Shape2DObject; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Sprite2DParamsType Type
        {
            get { return m_Type; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="ob_"></param>
        public Sprite2DParams(Sprite2DParamsType type_, Shape2DObject ob_)
        {
            m_Type = type_;
            m_Shape2DObject = ob_;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_"></param>
        /// <param name="opt_"></param>
        public void Load(BinaryReader br_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

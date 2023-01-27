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
    public partial class Sprite2DParams
        : ISaveLoad
    {
        #region Fields

        private object m_Tag;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public object Tag
        {
            get { return m_Tag; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="ob_"></param>
        /// <param name="tag_"></param>
        public Sprite2DParams(Sprite2DParamsType type_, Shape2DObject ob_, object tag_)
            : this(type_, ob_)
        {
            m_Tag = tag_;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(XmlElement el_, SaveOption option_)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_"></param>
        /// <param name="opt_"></param>
        public void Save(BinaryWriter bw_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

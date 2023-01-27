using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XNAFinalEngine.UserInterface;
using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.UI
{
    public partial class SkinUI
    {
        #region Fields

        #endregion

        #region Properties

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public SkinUI()
        {

        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion       
    }
}

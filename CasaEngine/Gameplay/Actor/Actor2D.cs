using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using FarseerPhysics.Common;
using CasaEngine.Gameplay.Actor.Object;
using FarseerPhysics.Dynamics;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class Actor2D
         : BaseObject, CasaEngineCommon.Design.IUpdateable
    {
        #region Fields

        //protected Body m_Body;
        private Vector2 m_Position = new Vector2();

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public Vector2 Position
        {
            get { return m_Position; }
            set { m_Position = value; }
        }

        /// <summary>
        /// Gets/Sets. If true the object will be remove from the current world
        /// </summary>
        public bool Delete
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        protected Actor2D()
            : base()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        protected Actor2D(XmlElement el_, SaveOption opt_)
            : base(el_, opt_)
        {

        }

        #endregion

        #region Methods

        #endregion

        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

        public virtual void Update(float elapsedTime_)
        {

        }
    }
}

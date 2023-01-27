using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Design;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// 
    /// </summary>
    public class BackgroundActor
        : Actor2D, IRenderable
    {
        #region Fields

        #endregion

        #region Properties

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Depth
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public float ZOrder
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public bool Visible
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public BackgroundActor()
            : base()
        {

        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

#if EDITOR
        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime"></param>
        public override void Update(float elapsedTime)
        {
            base.Update(elapsedTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public void Draw(float elapsedTime_)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

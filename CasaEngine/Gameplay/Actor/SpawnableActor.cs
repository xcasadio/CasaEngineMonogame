using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// 
    /// </summary>
    public class SpawnableActor
        : Actor2D
    {
        #region Fields

        #endregion

        #region Properties

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public SpawnableActor()
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

        #endregion
    }
}

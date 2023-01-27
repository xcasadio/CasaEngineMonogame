using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FarseerPhysics.Common;
using CasaEngine.Math.Shape2D;
using FarseerPhysics.Dynamics;
using CasaEngineCommon.Pool;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
	/// <summary>
	/// 
	/// </summary>
	public class Animation2DPlayer
	{
		#region Fields

#if !FINAL
        /// <summary>
        /// Use for debugging
        /// </summary>
        static public float AnimationSpeed = 1.0f;
#endif

        private Dictionary<int, Animation2D> m_Animations;
        private Animation2D m_CurrentAnimation = null;

		public event EventHandler OnEndAnimationReached;
        public event EventHandler<Animation2DFrameChangedEventArgs> OnFrameChanged;

        /// <summary>
        /// use to detected if we change animation
        /// </summary>
        private int m_CurrentAnimationIndex = -1;

		#endregion

		#region Properties

		/// <summary>
		/// Gets current animation
		/// </summary>
        public Animation2D CurrentAnimation
		{
			get { return m_CurrentAnimation; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="animations_">List of all animation handle by the player</param>
		public Animation2DPlayer(Dictionary<int, Animation2D> animations_)
		{
            m_Animations = animations_;

            foreach (KeyValuePair<int, Animation2D> pair in animations_)
            {
                pair.Value.OnFrameChanged += new EventHandler<Animation2DFrameChangedEventArgs>(FrameChanging);
                pair.Value.OnEndAnimationReached += new EventHandler(EventHandler_OnEndAnimationReached);
            }
		}    

		#endregion

		#region Methods

        /// <summary>
        /// Call when a new frame index is reached from the current animation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrameChanging(object sender, Animation2DFrameChangedEventArgs e)
        {
            if (OnFrameChanged != null)
            {
                OnFrameChanged.Invoke(sender, e);
            }
        }

        /// <summary>
        /// Call when the end of the current animation is reached
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EventHandler_OnEndAnimationReached(object sender, EventArgs e)
        {
 	        if (OnEndAnimationReached != null)
            {
                OnEndAnimationReached.Invoke(sender, e);
            }
        } 

        /// <summary>
        /// set the animation by id
        /// Change animation only if different with the current animation and call Animation2D.ResetTime()
        /// </summary>
        /// <param name="id_"></param>
		public void SetCurrentAnimationByID(int id_)
        {
            if (m_CurrentAnimationIndex != id_)
            {
                m_CurrentAnimationIndex = id_;
                m_CurrentAnimation = m_Animations[id_];
                m_CurrentAnimation.ResetTime();
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        public void SetCurrentAnimationByName(string name_)
        {
            int index = -1;

            foreach (KeyValuePair<int, Animation2D> pair in m_Animations)
            {
                if (pair.Value.Name.Equals(name_) == true)
                {
                    index = pair.Key;
                }
            }

            if (m_CurrentAnimationIndex != index)
            {
                m_CurrentAnimationIndex = index;
                m_CurrentAnimation = m_Animations[index];
                m_CurrentAnimation.ResetTime();
            }
        }        

        /// <summary>
        /// Update animation time
        /// Call Animation2D.Update()
        /// </summary>
        /// <param name="elpasedTime_"></param>
        public void Update(float elpasedTime_)
        {
#if !FINAL
            m_CurrentAnimation.Update(elpasedTime_ * AnimationSpeed);
#else
            m_CurrentAnimation.Update(elpasedTime_);
#endif
        }

		#endregion
	}
}

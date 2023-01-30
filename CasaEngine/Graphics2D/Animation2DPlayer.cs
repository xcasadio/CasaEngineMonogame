using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
    public class Animation2DPlayer
    {

#if !FINAL
        static public float AnimationSpeed = 1.0f;
#endif

        private readonly Dictionary<int, Animation2D> m_Animations;
        private Animation2D m_CurrentAnimation = null;

        public event EventHandler OnEndAnimationReached;
        public event EventHandler<Animation2DFrameChangedEventArgs> OnFrameChanged;

        private int m_CurrentAnimationIndex = -1;



        public Animation2D CurrentAnimation => m_CurrentAnimation;


        public Animation2DPlayer(Dictionary<int, Animation2D> animations_)
        {
            m_Animations = animations_;

            foreach (KeyValuePair<int, Animation2D> pair in animations_)
            {
                pair.Value.OnFrameChanged += new EventHandler<Animation2DFrameChangedEventArgs>(FrameChanging);
                pair.Value.OnEndAnimationReached += new EventHandler(EventHandler_OnEndAnimationReached);
            }
        }



        private void FrameChanging(object sender, Animation2DFrameChangedEventArgs e)
        {
            if (OnFrameChanged != null)
            {
                OnFrameChanged.Invoke(sender, e);
            }
        }

        private void EventHandler_OnEndAnimationReached(object sender, EventArgs e)
        {
            if (OnEndAnimationReached != null)
            {
                OnEndAnimationReached.Invoke(sender, e);
            }
        }

        public void SetCurrentAnimationByID(int id_)
        {
            if (m_CurrentAnimationIndex != id_)
            {
                m_CurrentAnimationIndex = id_;
                m_CurrentAnimation = m_Animations[id_];
                m_CurrentAnimation.ResetTime();
            }
        }

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

        public void Update(float elpasedTime_)
        {
#if !FINAL
            m_CurrentAnimation.Update(elpasedTime_ * AnimationSpeed);
#else
            m_CurrentAnimation.Update(elpasedTime_);
#endif
        }

    }
}

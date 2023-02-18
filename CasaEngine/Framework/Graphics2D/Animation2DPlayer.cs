using CasaEngine.Framework.Assets.Graphics2D;

namespace CasaEngine.Framework.Graphics2D
{
    public class Animation2DPlayer
    {

#if !FINAL
        public static float AnimationSpeed = 1.0f;
#endif

        private readonly Dictionary<int, Animation2D> _animations;
        private Animation2D _currentAnimation;

        public event EventHandler? OnEndAnimationReached;
        public event EventHandler<Animation2DFrameChangedEventArgs>? OnFrameChanged;

        private int _currentAnimationIndex = -1;



        public Animation2D CurrentAnimation => _currentAnimation;


        public Animation2DPlayer(Dictionary<int, Animation2D> animations)
        {
            _animations = animations;

            foreach (var pair in animations)
            {
                pair.Value.OnFrameChanged += FrameChanging;
                pair.Value.OnEndAnimationReached += EventHandler_OnEndAnimationReached;
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

        public void SetCurrentAnimationById(int id)
        {
            if (_currentAnimationIndex != id)
            {
                _currentAnimationIndex = id;
                _currentAnimation = _animations[id];
                _currentAnimation.ResetTime();
            }
        }

        public void SetCurrentAnimationByName(string name)
        {
            var index = -1;

            foreach (var pair in _animations)
            {
                if (pair.Value.Name.Equals(name))
                {
                    index = pair.Key;
                }
            }

            if (_currentAnimationIndex != index)
            {
                _currentAnimationIndex = index;
                _currentAnimation = _animations[index];
                _currentAnimation.ResetTime();
            }
        }

        public void Update(float elpasedTime)
        {
#if !FINAL
            _currentAnimation.Update(elpasedTime * AnimationSpeed);
#else
            _CurrentAnimation.Update(elpasedTime_);
#endif
        }

    }
}

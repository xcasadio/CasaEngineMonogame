namespace CasaEngine.Framework.Assets.Graphics2D
{
    public class Animation2DFrameChangedEventArgs
        : EventArgs
    {
        Animation2D _animation2D;
        int _oldFrame, _newFrame;

        public Animation2D Animation2D
        {
            get => _animation2D;
            internal set => _animation2D = value;
        }

        public int OldFrame
        {
            get => _oldFrame;
            internal set => _oldFrame = value;
        }

        public int NewFrame
        {
            get => _newFrame;
            internal set => _newFrame = value;
        }

        public Animation2DFrameChangedEventArgs(Animation2D anim2D, int oldFrame, int newFrame)
        {
            _animation2D = anim2D;
            _oldFrame = oldFrame;
            _newFrame = newFrame;
        }
    }
}

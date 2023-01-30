namespace CasaEngine.Assets.Graphics2D
{
    public class Animation2DFrameChangedEventArgs
        : EventArgs
    {
        Animation2D m_Animation2D;
        int m_OldFrame, m_NewFrame;

        public Animation2D Animation2D
        {
            get => m_Animation2D;
            internal set => m_Animation2D = value;
        }

        public int OldFrame
        {
            get => m_OldFrame;
            internal set => m_OldFrame = value;
        }

        public int NewFrame
        {
            get => m_NewFrame;
            internal set => m_NewFrame = value;
        }

        public Animation2DFrameChangedEventArgs(Animation2D anim2D_, int oldFrame_, int newFrame_)
        {
            m_Animation2D = anim2D_;
            m_OldFrame = oldFrame_;
            m_NewFrame = newFrame_;
        }
    }
}

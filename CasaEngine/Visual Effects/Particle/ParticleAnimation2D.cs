using CasaEngine.Graphics2D;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Particle
{
    public class ParticleAnimation2D
        : Particle
    {

        Animation2DPlayer m_Animation2DPlayer;
        //Animation2D m_Animation2D;





        public ParticleAnimation2D(Animation2D anim2D_)
        {
            Dictionary<int, Animation2D> dic = new Dictionary<int, Animation2D>();
            dic.Add(0, anim2D_);
            m_Animation2DPlayer = new Animation2DPlayer(dic);
            m_Animation2DPlayer.OnEndAnimationReached += new EventHandler(OnEndAnimationReached);
        }

        void OnEndAnimationReached(object sender, EventArgs e)
        {
            Remove = true;
        }



    }
}

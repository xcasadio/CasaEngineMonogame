using CasaEngine.Graphics2D;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Particle
{
    public class ParticleAnimation2D
        : Particle
    {

        Animation2DPlayer _animation2DPlayer;
        //Animation2D _Animation2D;





        public ParticleAnimation2D(Animation2D anim2D)
        {
            Dictionary<int, Animation2D> dic = new Dictionary<int, Animation2D>();
            dic.Add(0, anim2D);
            _animation2DPlayer = new Animation2DPlayer(dic);
            _animation2DPlayer.OnEndAnimationReached += new EventHandler(OnEndAnimationReached);
        }

        void OnEndAnimationReached(object sender, EventArgs e)
        {
            Remove = true;
        }



    }
}

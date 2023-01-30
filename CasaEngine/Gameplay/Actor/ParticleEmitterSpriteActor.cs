using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public class ParticleEmitterActor
        : Actor2D
    {





        public ParticleEmitterActor()
            : base()
        {

        }



        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

#if EDITOR
        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }
#endif

    }
}

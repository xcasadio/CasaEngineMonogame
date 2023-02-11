using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public class SoundActor2D
        : Actor2D
    {

        //SoundEffect 
        //SoundEffectInstance





        public SoundActor2D()
            : base()
        {

        }



        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

#if EDITOR
        public override bool CompareTo(BaseObject other)
        {
            throw new NotImplementedException();
        }
#endif

    }
}

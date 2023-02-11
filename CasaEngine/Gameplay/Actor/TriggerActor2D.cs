using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public class TriggerActor2D
        : Actor2D
    {





        public TriggerActor2D()
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

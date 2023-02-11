using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public class SkinnedSpriteActor
        : Actor2D
    {





        public SkinnedSpriteActor()
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

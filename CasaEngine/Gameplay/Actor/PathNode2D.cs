using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    class PathNode2D
        : Actor2D
    {





        public PathNode2D()
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

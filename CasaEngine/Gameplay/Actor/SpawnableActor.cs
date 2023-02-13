namespace CasaEngine.Gameplay.Actor
{
    public class SpawnableActor
        : Actor2D
    {





        public SpawnableActor()
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

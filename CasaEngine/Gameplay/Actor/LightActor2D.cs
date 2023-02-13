namespace CasaEngine.Gameplay.Actor
{
    public class LightActor2D
        : Actor2D
    {





        public LightActor2D()
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

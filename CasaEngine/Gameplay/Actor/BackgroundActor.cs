using CasaEngine.Gameplay.Actor.Object;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    public class BackgroundActor
        : Actor2D, IRenderable
    {



        public int Depth
        {
            get;
            set;
        }

        public float ZOrder
        {
            get;
            set;
        }

        public bool Visible
        {
            get;
            set;
        }



        public BackgroundActor()
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

        public override void Update(float elapsedTime)
        {
            base.Update(elapsedTime);
        }

        public void Draw(float elapsedTime)
        {
            throw new NotImplementedException();
        }

    }
}

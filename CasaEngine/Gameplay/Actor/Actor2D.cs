using Microsoft.Xna.Framework;
using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    public abstract class Actor2D
         : BaseObject, CasaEngineCommon.Design.IUpdateable
    {

        //protected Body _Body;
        private Vector2 _position = new Vector2();



        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public bool Delete
        {
            get;
            set;
        }



        protected Actor2D()
            : base()
        {

        }

        protected Actor2D(XmlElement el, SaveOption opt)
            : base(el, opt)
        {

        }




        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

        public virtual void Update(float elapsedTime)
        {

        }
    }
}

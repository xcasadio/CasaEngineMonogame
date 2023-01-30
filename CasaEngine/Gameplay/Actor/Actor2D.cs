using Microsoft.Xna.Framework;
using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    public abstract class Actor2D
         : BaseObject, CasaEngineCommon.Design.IUpdateable
    {

        //protected Body m_Body;
        private Vector2 m_Position = new Vector2();



        public Vector2 Position
        {
            get => m_Position;
            set => m_Position = value;
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

        protected Actor2D(XmlElement el_, SaveOption opt_)
            : base(el_, opt_)
        {

        }




        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

        public virtual void Update(float elapsedTime_)
        {

        }
    }
}

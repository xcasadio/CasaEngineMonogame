using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngine.Entities;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
    public abstract class Actor2D : Entity
    {

        //protected Body _Body;
        private Vector2 _position;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        protected Actor2D()
            : base()
        {

        }

        protected Actor2D(XmlElement el, SaveOption opt)
            : base(el, opt)
        {

        }

        public Entity Clone()
        {
            throw new NotImplementedException();
        }

        public virtual void Update(float elapsedTime)
        {

        }
    }
}

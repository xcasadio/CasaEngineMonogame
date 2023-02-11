#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.AI
{
    [Serializable]
    public abstract class BaseEntity
    {

        public const int EntityNotRegistered = -1;



        protected internal int Id = BaseEntity.EntityNotRegistered; // TODO: remove ? ObjectContainer already conatins ID

        protected internal bool Remove;



#if EDITOR
        [Category("Object"), ReadOnly(true)]
#endif
        public int Id
        {
            get
            {
                return Id;
            }
            set
            {
                Id = value;
            }
        }

#if EDITOR
        [Browsable(false)]
#endif
        public bool Remove
        {
            get { return remove; }
            set { remove = value; }
        }



        public BaseEntity()
        {
            remove = false;
            //EntityManager.Instance.AddEntity(this);
        }



        //public abstract void Update(float elapsedTime);

        protected virtual void Destroy()
        { }

    }
}

#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.AI
{
    [Serializable]
    public abstract class BaseEntity
    {
        public const int EntityNotRegistered = -1;

#if EDITOR
        [Category("Object"), ReadOnly(true)]
#endif
        public int Id
        {
            get;
            set;
        } = EntityNotRegistered;

#if EDITOR
        [Browsable(false)]
#endif
        public bool Remove { get; set; }

        public BaseEntity()
        {
            Remove = false;
            //EntityManager.Instance.AddEntity(this);
        }

        //public abstract void Update(float elapsedTime);

        protected virtual void Destroy()
        { }
    }
}

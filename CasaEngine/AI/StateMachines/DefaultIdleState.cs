namespace CasaEngine.AI.StateMachines
{
    [Serializable]
    public class DefaultIdleState<T> : IState<T> where T : /*BaseEntity,*/ IFSMCapable<T>
    {

        public string Name => "DefaultIdleState";

        public void Enter(T entity) { }

        public void Exit(T entity) { }

        public void Update(T entity, float elpasedTime_) { }

        public bool HandleMessage(T entity, Message message)
        {
            return false;
        }

    }
}

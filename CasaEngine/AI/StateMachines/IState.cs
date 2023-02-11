namespace CasaEngine.AI.StateMachines
{
    public interface IState<T> where T : /*BaseEntity,*/ IFsmCapable<T>
    {

        string Name { get; }

        void Enter(T entity);

        void Exit(T entity);

        void Update(T entity, float elpasedTime);

        bool HandleMessage(T entity, Message message);

    }
}

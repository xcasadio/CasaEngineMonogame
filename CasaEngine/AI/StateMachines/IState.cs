namespace CasaEngine.AI.StateMachines
{
    public interface IState<T>
    {
        string Name { get; }

        void Enter(T entity);
        void Exit(T entity);
        void Update(T entity, float elapsedTime);
        bool HandleMessage(T entity, Message message);
    }
}

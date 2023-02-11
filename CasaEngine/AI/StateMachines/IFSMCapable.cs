namespace CasaEngine.AI.StateMachines
{
    public interface IFsmCapable<T> where T : IFsmCapable<T>
    {
        IFiniteStateMachine<T> StateMachine
        {
            get;
            set;
        }
    }
}

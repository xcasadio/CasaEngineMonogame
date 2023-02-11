namespace CasaEngine.AI.StateMachines
{
    public interface IFsmCapable<T> where T : /*BaseEntity,*/ IFsmCapable<T>
    {

        IFiniteStateMachine<T> StateMachine
        {
            get;
            set;
        }

    }
}

namespace CasaEngine.AI.StateMachines
{
    public interface IFSMCapable<T> where T : /*BaseEntity,*/ IFSMCapable<T>
    {

        IFiniteStateMachine<T> StateMachine
        {
            get;
            set;
        }

    }
}

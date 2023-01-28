
using System;


using System.Collections.Generic;




namespace CasaEngine.AI.StateMachines
{
    /// <summary>
    /// Interface that indicates that the entity owns a finite state machine
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IFSMCapable<T> where T : /*BaseEntity,*/ IFSMCapable<T>
    {

        /// <summary>
        /// Gets or sets the finite state machine
        /// </summary>
        IFiniteStateMachine<T> StateMachine
        {
            get;
            set;
        }

    }
}

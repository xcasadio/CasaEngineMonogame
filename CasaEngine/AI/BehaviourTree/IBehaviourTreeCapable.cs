using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace CasaEngine.AI.BehaviourTree
{
	/// <summary>
	/// Interface that indicates that the entity owns a finite state machine
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IBehaviourTreeCapable<T> where T : BaseEntity, IBehaviourTreeCapable<T>
	{
		#region Properties

		/// <summary>
		/// Gets or sets the finite state machine
		/// </summary>
		BehaviourTree<T> StateMachine
		{
			get;
			set;
		}

		#endregion
	}
}

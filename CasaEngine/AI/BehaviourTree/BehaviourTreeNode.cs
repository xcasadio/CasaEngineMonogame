using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CasaEngine.AI.Messaging;

namespace CasaEngine.AI.BehaviourTree
{
	/// <summary>
	/// 
	/// </summary>
	public abstract class BehaviourTreeNode<T> 
		: BaseEntity
	{
		#region Fields

		string m_Name;
		BehaviourTreeNode<T> m_Parent = null;
		List<BehaviourTreeNode<T>> m_Children = new List<BehaviourTreeNode<T>>();
		//condition script
		
		#endregion

		#region Properties

		/// <summary>
		/// Gets name
		/// </summary>
		public string Name
		{
			get { return m_Name; }
		}

		/// <summary>
		/// Gets parent node
		/// </summary>
		public BehaviourTreeNode<T> Parent
		{
			get { return m_Parent; }
		}

		/// <summary>
		/// Gets children nodes
		/// </summary>
		public List<BehaviourTreeNode<T>> Children
		{
			get { return m_Children; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		public BehaviourTreeNode(string name_)
		{
			m_Name = name_;
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		public abstract bool EnterCondition(T entity);

		/// <summary>
		/// The action that happens when the state is first entered
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		public abstract void Enter(T entity);

		/// <summary>
		/// The action that happens when the state finishes
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		public abstract void Exit(T entity);

		/// <summary>
		/// The action executed everytime the state is update
		/// </summary>
		/// <param name="entity">The entity that changed state</param>
		public abstract void Update(T entity);

		/// <summary>
		/// Function that will handle the messages sent to the state from the state machine
		/// </summary>
		/// <param name="entity">The entity that owns the state</param>
		/// <param name="message">Message sent to the entity</param>
		/// <returns>True if the state could handle the message, false if not</returns>
		public abstract bool HandleMessage(T entity, Message message);

		#endregion
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace CasaEngine.AI.BehaviourTree
{
	/// <summary>
	/// The Behaviour Tree is a HFSM 
	/// </summary>
	public class BehaviourTree<T> where T : BaseEntity 
	{
		#region Fields

		/// <summary>
		/// The owner of the state machine
		/// </summary>
		protected internal T m_Owner;

		BehaviourTreeNode<T> m_Root = null;
		BehaviourTreeNode<T> m_CurrentNode = null;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="elapsedTime"></param>
		public void Update()
		{
			if (m_Root == null) return;

			if (m_Root.EnterCondition(m_Owner) == false)
			{
				if (m_CurrentNode != null)
				{
					m_CurrentNode.Exit(m_Owner);
				}

				m_CurrentNode = null; //rester dans l'etat ??
			}

			BehaviourTreeNode<T> node = Update(m_Root.Children);

			if (node == null)
			{
				if (m_CurrentNode != null)
					m_CurrentNode.Exit(m_Owner);

				m_CurrentNode = null;
			}
			else
			{
				if (m_CurrentNode != node)
				{
					m_CurrentNode.Exit(m_Owner);
					node.Enter(m_Owner);
					m_CurrentNode = node;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="nodes_"></param>
		/// <returns></returns>
		BehaviourTreeNode<T> Update(List<BehaviourTreeNode<T>> nodes_)
		{
			foreach(BehaviourTreeNode<T> node in nodes_)
			{
				if (node.EnterCondition(m_Owner) == true)
				{
					if (node.Children.Count == 0)
					{
						return node;
					}

					return Update(node.Children);
				}
			}

			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <returns></returns>
		public BehaviourTreeNode<T> GetNodeByName(string name_)
		{
			if (m_Root == null)
				return null;

			if (m_Root.Name.Equals(name_))
				return m_Root;

			return SearchNodeByName(name_, m_Root.Children);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <param name="nodes_"></param>
		/// <returns></returns>
		BehaviourTreeNode<T> SearchNodeByName(string name_, List<BehaviourTreeNode<T>> nodes_)
		{
			foreach (BehaviourTreeNode<T> node in nodes_)
			{
				if (node.Name.Equals(name_))
				{
					return node;
				}

				return SearchNodeByName(name_, node.Children);
			}

			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parentNode_"></param>
		/// <param name="node_"></param>
		public void AddNode(string parentNodeName_, BehaviourTreeNode<T> node_)
		{
			if (node_ == null)
			{
				throw new ArgumentNullException("BehaviourTree<T>.AddNode() : node is null");
			}

			if (string.IsNullOrEmpty(parentNodeName_))
			{
				m_Root = node_;
			}

			BehaviourTreeNode<T> parent = GetNodeByName(parentNodeName_);

			if (parent == null)
			{
				throw new InvalidOperationException("BehaviourTree<T>.AddNode() : node named " + parentNodeName_ + " is unknown");
			}

			parent.Children.Add(node_);
		}

		#endregion
	}
}

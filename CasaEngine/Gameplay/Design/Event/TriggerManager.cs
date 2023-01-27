
#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CasaEngine.Design.Event
{
	/// <summary>
	/// 
	/// </summary>
	public class TriggerManager
	{
		#region Fields

		List<Trigger> m_Triggers = new List<Trigger>();

		#endregion

		#region Properties

		/// <summary>
		/// Gets
		/// </summary>
		public List<Trigger> Triggers
		{
			get { return m_Triggers; }
		}

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		public void Update(float elapsedTime_)
		{
			foreach (Trigger t in m_Triggers.ToArray())
			{
				t.Update(elapsedTime_);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void Reset()
		{
			foreach (Trigger t in m_Triggers.ToArray())
			{
				t.Reset();
			}
		}

		#endregion
	}
}

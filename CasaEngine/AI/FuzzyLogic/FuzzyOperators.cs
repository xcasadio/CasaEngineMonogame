using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// 
	/// </summary>
	public class FzAND
		: FuzzyTerm
	{
		#region Fields

		//an instance of this class may AND together up to 4 terms
		List<FuzzyTerm> m_Terms = new List<FuzzyTerm>();

		//disallow assignment
		//FzAND operator=(FzAND&);

		#endregion

		#region Properties

		/// <summary>
		/// the AND operator returns the minimum DOM of the sets it is operating on
		/// </summary>
		public double DOM
		{
			get
			{
				double smallest = double.MaxValue;

				foreach (FuzzyTerm t in m_Terms)
				{
					if (t.DOM < smallest)
					{
						smallest = t.DOM;
					}
				}

				return smallest;
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Implementation of FzAND
		/// </summary>
		/// <param name="fa"></param>
		public FzAND(FzAND fa)
		{
			foreach(FuzzyTerm f in fa.m_Terms)
			{
				m_Terms.Add(f.Clone());
			}
		}

		/// <summary>
		/// ctor using two terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		public FzAND(FuzzyTerm op1, FuzzyTerm op2)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
		}

		/// <summary>
		/// ctor using three terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <param name="op3"></param>
		public FzAND(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
			m_Terms.Add(op3.Clone());
		}

		/// <summary>
		/// ctor using four terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <param name="?"></param>
		public FzAND(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3, FuzzyTerm op4)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
			m_Terms.Add(op3.Clone());
			m_Terms.Add(op4.Clone());
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="val"></param>
		public void ORwithDOM(double val)
		{
			foreach (FuzzyTerm t in m_Terms)
			{
				t.ORwithDOM(val);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void ClearDOM()
		{
			foreach (FuzzyTerm t in m_Terms)
			{
				t.ClearDOM();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public FuzzyTerm Clone()
		{
			return new FzAND(this);
		}

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class FzOR
		: FuzzyTerm
	{
		#region Fields

		//an instance of this class may AND together up to 4 terms
		List<FuzzyTerm> m_Terms = new List<FuzzyTerm>();

		//disallow assignment
		//FzAND operator=(FzAND&);

		#endregion

		#region Properties

		/// <summary>
		/// Gets the OR operator returns the maximum DOM of the sets it is operating on
		/// </summary>
		public double DOM
		{
			get
			{
				double largest = float.MinValue;

				foreach (FuzzyTerm t in m_Terms)
				{
					if (t.DOM > largest)
					{
						largest = t.DOM;
					}
				}

				return largest;
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Implementation of FzAND
		/// </summary>
		/// <param name="fa"></param>
		public FzOR(FzOR fa)
		{
			foreach (FuzzyTerm f in fa.m_Terms)
			{
				m_Terms.Add(f.Clone());
			}
		}

		/// <summary>
		/// ctor using two terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		public FzOR(FuzzyTerm op1, FuzzyTerm op2)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
		}

		/// <summary>
		/// ctor using three terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <param name="op3"></param>
		public FzOR(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
			m_Terms.Add(op3.Clone());
		}

		/// <summary>
		/// ctor using four terms
		/// </summary>
		/// <param name="op1"></param>
		/// <param name="op2"></param>
		/// <param name="?"></param>
		public FzOR(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3, FuzzyTerm op4)
		{
			m_Terms.Add(op1.Clone());
			m_Terms.Add(op2.Clone());
			m_Terms.Add(op3.Clone());
			m_Terms.Add(op4.Clone());
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		public void ClearDOM()
		{
			throw new InvalidOperationException("FzOR.ClearDOM() : invalid context");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="val"></param>
		public void ORwithDOM(double val)
		{
			throw new InvalidOperationException("FzOR.ORwithDOM() : invalid context");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public FuzzyTerm Clone()
		{
			return new FzOR(this);
		}

		#endregion
	}
}

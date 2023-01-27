using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngineCommon.Extension;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class CalculatorTokenBinaryOperator
		: ICalculatorToken
	{
		/// <summary>
		/// 
		/// </summary>
		public enum BinaryOperator
		{
			Plus,
			Minus,
			Multiply,
			Divide,

			Equal,
			Different,
			Superior,
			Inferior,
			Or,
			And,
			SupEqual,
			InfEqual,
		}

		#region Fields

		BinaryOperator m_Operator;
		ICalculatorToken m_Left;		
		ICalculatorToken m_Right;

        #endregion

        #region Properties

		/// <summary>
		/// 
		/// </summary>
		public ICalculatorToken Left
		{
			get { return m_Left; }
			set { m_Left = value; }
		}

		/// <summary>
		/// 
		/// </summary>
		public ICalculatorToken Right
		{
			get { return m_Right; }
			set { m_Right = value; }
		}

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="operator_"></param>
		public CalculatorTokenBinaryOperator(Calculator calculator_, BinaryOperator operator_)
			: base(calculator_)
		{
			m_Operator = operator_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public CalculatorTokenBinaryOperator(Calculator calculator_, XmlElement el_, SaveOption option_)
			: base(calculator_)
		{
			Load(el_, option_);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public CalculatorTokenBinaryOperator(Calculator calculator_, BinaryReader br_, SaveOption option_)
            : base(calculator_)
        {
            Load(br_, option_);
        }

        #endregion

        #region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override float Evaluate()
		{
			float res;			

			switch (m_Operator)
			{
				case BinaryOperator.Plus:
					res = m_Left.Evaluate() + m_Right.Evaluate();
					break;

				case BinaryOperator.Minus:
					res = m_Left.Evaluate() - m_Right.Evaluate();
					break;

				case BinaryOperator.Multiply:
					res = m_Left.Evaluate() * m_Right.Evaluate();
					break;

				case BinaryOperator.Divide:
					res = m_Left.Evaluate() / m_Right.Evaluate();
					break;

				case BinaryOperator.Equal:
					res = System.Convert.ToSingle(m_Left.Evaluate() == m_Right.Evaluate());
					break;

				case BinaryOperator.Different:
					res = System.Convert.ToSingle(m_Left.Evaluate() != m_Right.Evaluate());
					break;

				case BinaryOperator.Superior:
					res = System.Convert.ToSingle(m_Left.Evaluate() > m_Right.Evaluate());
					break;

				case BinaryOperator.Inferior:
					res = System.Convert.ToSingle(m_Left.Evaluate() < m_Right.Evaluate());
					break;

				case BinaryOperator.SupEqual:
					res = System.Convert.ToSingle(m_Left.Evaluate() >= m_Right.Evaluate());
					break;

				case BinaryOperator.InfEqual:
					res = System.Convert.ToSingle(m_Left.Evaluate() <= m_Right.Evaluate());
					break;

				case BinaryOperator.Or:
					res = System.Convert.ToSingle(System.Convert.ToBoolean(m_Left.Evaluate()) || System.Convert.ToBoolean(m_Right.Evaluate()));
					break;

				case BinaryOperator.And:
					res = System.Convert.ToSingle(System.Convert.ToBoolean(m_Left.Evaluate()) && System.Convert.ToBoolean(m_Right.Evaluate()));
					break;

				default:
					throw new InvalidOperationException("CalculatorTokenBinaryOperator.Evaluate() : BinaryOperator unknown");
			}

			return res;
		}

		#region Save / Load

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public override void Save(XmlElement el_, SaveOption option_)
		{
			XmlElement node = (XmlElement)el_.OwnerDocument.CreateElement("Node");
			el_.AppendChild(node);
			el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.BinaryOperator).ToString());
			el_.OwnerDocument.AddAttribute(node, "operator", ((int)m_Operator).ToString());

			XmlElement child = (XmlElement)el_.OwnerDocument.CreateElement("Left");
			node.AppendChild(child);
			m_Left.Save(child, option_);

			child = (XmlElement)el_.OwnerDocument.CreateElement("Right");
			node.AppendChild(child);
			m_Right.Save(child, option_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public override void Load(XmlElement el_, SaveOption option_)
		{
			m_Operator = (BinaryOperator) int.Parse(el_.Attributes["operator"].Value);

			XmlElement child = (XmlElement)el_.SelectSingleNode("Left/Node");
			m_Left = Calculator.CreateCalculatorToken(Calculator, child, option_);

			child = (XmlElement)el_.SelectSingleNode("Right/Node");
			m_Right = Calculator.CreateCalculatorToken(Calculator, child, option_);

		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.BinaryOperator);
            bw_.Write((int)m_Operator);
            m_Left.Save(bw_, option_);
            m_Right.Save(bw_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            m_Operator = (BinaryOperator)br_.ReadInt32();
            m_Left = Calculator.CreateCalculatorToken(Calculator, br_, option_);
            m_Right = Calculator.CreateCalculatorToken(Calculator, br_, option_);
        }

		#endregion

		#endregion
		
	}
}

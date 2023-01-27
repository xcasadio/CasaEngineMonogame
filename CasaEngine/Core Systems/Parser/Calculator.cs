using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class Calculator
	{
		#region Fields

		ICalculatorToken m_Root;
		Parser m_Parser;
		
        #endregion

        #region Properties

		/// <summary>
		/// 
		/// </summary>
		public ICalculatorToken Root
		{
			get { return m_Root; }
			set { m_Root = value; }
		}

		/// <summary>
		/// 
		/// </summary>
		public CasaEngine.Design.Parser.Parser Parser
		{
			get { return m_Parser; }
		}

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="parser_"></param>
		public Calculator(Parser parser_)
		{
			m_Parser = parser_;
		}

        #endregion

        #region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public float Evaluate()
		{
			return m_Root.Evaluate();
		}

		#region Save / Load

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public void Load(XmlElement el_, SaveOption option_)
		{
			m_Root = null;

			XmlElement root = (XmlElement) el_.SelectSingleNode("Root/Node");

			if (root != null)
			{
				m_Root = CreateCalculatorToken(this, root, option_);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public void Save(XmlElement el_, SaveOption option_)
		{
			if (m_Root != null)
			{
				XmlNode node = el_.OwnerDocument.CreateElement("Root");
				el_.AppendChild(node);
				m_Root.Save((XmlElement)node, option_);
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            if (m_Root != null)
            {
                m_Root.Save(bw_, option_);
            }
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="token_"></param>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		/// <returns></returns>
		static public ICalculatorToken CreateCalculatorToken(Calculator calculator_, XmlElement el_, SaveOption option_)
		{
			ICalculatorToken token = null;

			CalculatorTokenType type = (CalculatorTokenType)int.Parse(el_.Attributes["type"].Value);

			switch (type)
			{
				case CalculatorTokenType.BinaryOperator:
					token = new CalculatorTokenBinaryOperator(calculator_, el_, option_);
					break;

				case CalculatorTokenType.Keyword:
					token = new CalculatorTokenKeyword(calculator_, el_, option_);
					break;

				/*case CalculatorTokenType.UnaryOperator:
					token = new CalculatorTokenUnaryOperator(el_, option_);
					break;*/

				case CalculatorTokenType.Value:
					token = new CalculatorTokenValue(calculator_, el_, option_);
					break;

				case CalculatorTokenType.Function:
					token = new CalculatorTokenFunction(calculator_, el_, option_);
					break;

				default:
					throw new InvalidOperationException("unknown CalculatorTokenType");
			}

			return token;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token_"></param>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        /// <returns></returns>
        static public ICalculatorToken CreateCalculatorToken(Calculator calculator_, BinaryReader br_, SaveOption option_)
        {
            ICalculatorToken token = null;

            CalculatorTokenType type = (CalculatorTokenType)br_.ReadInt32();

            switch (type)
            {
                case CalculatorTokenType.BinaryOperator:
                    token = new CalculatorTokenBinaryOperator(calculator_, br_, option_);
                    break;

                case CalculatorTokenType.Keyword:
                    token = new CalculatorTokenKeyword(calculator_, br_, option_);
                    break;

                /*case CalculatorTokenType.UnaryOperator:
                    token = new CalculatorTokenUnaryOperator(el_, option_);
                    break;*/

                case CalculatorTokenType.Value:
                    token = new CalculatorTokenValue(calculator_, br_, option_);
                    break;

                case CalculatorTokenType.Function:
                    token = new CalculatorTokenFunction(calculator_, br_, option_);
                    break;

                default:
                    throw new InvalidOperationException("unknown CalculatorTokenType");
            }

            return token;
        }

		#endregion

		#endregion
	}
}

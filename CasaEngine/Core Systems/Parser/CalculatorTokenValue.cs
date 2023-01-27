using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
	/// <summary>
	/// 
	/// </summary>
	class CalculatorTokenValue
		: ICalculatorToken
	{
		#region Fields

		int m_Type;
		float m_Value;
		string m_String;

        #endregion

        #region Properties

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value_"></param>
		public CalculatorTokenValue(Calculator calculator_, float value_)
			: base(calculator_)
		{
			m_Value = value_;
			m_Type = 0;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="value_"></param>
		public CalculatorTokenValue(Calculator calculator_, string value_)
			: base(calculator_)
		{
			m_String = value_;
			m_Type = 1;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public CalculatorTokenValue(Calculator calculator_, XmlElement el_, SaveOption option_)
			: base(calculator_)
		{
			Load(el_, option_);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public CalculatorTokenValue(Calculator calculator_, BinaryReader br_, SaveOption option_)
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
			return m_Value;
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
			el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Value).ToString());

            string value = m_Type == 0 ? m_Value.ToString() : m_String;

            XmlElement valueNode = (XmlElement)el_.OwnerDocument.CreateElementWithText("Value", value);
			el_.OwnerDocument.AddAttribute(valueNode, "type", m_Type.ToString());
			node.AppendChild(valueNode);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public override void Load(XmlElement el_, SaveOption option_)
		{
			m_Type = int.Parse(el_.SelectSingleNode("Value").Attributes["type"].Value);
			if (m_Type == 0)
			{
				m_Value = float.Parse(el_.SelectSingleNode("Value").InnerText);
			}
			else
			{
				m_String = el_.SelectSingleNode("Value").InnerText;
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.Value);
            string value = m_Type == 0 ? m_Value.ToString() : m_String;
            bw_.Write(value);
            bw_.Write(m_Type);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            m_Type = br_.ReadInt32();

            if (m_Type == 0)
            {
                m_Value = float.Parse(br_.ReadString());
            }
            else
            {
                m_String = br_.ReadString();
            }
        }

		#endregion

        #endregion
	}
}

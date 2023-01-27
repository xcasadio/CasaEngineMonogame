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
	class CalculatorTokenFunction
		: ICalculatorToken
	{
		#region Fields

		string m_FunctionName;
		string[] m_Args;

        #endregion

        #region Properties

        #endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="calculator_"></param>
		/// <param name="functionName_"></param>
		/// <param name="args_"></param>
		public CalculatorTokenFunction(Calculator calculator_, string functionName_, string[] args_)
			: base(calculator_)
		{
			m_FunctionName = functionName_;
			m_Args = args_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public CalculatorTokenFunction(Calculator calculator_, XmlElement el_, SaveOption option_)
			: base(calculator_)
		{
			Load(el_, option_);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public CalculatorTokenFunction(Calculator calculator_, BinaryReader br_, SaveOption option_)
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
			return Calculator.Parser.EvaluateFunction(m_FunctionName, m_Args);
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
			el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Function).ToString());
			XmlElement valueNode = (XmlElement)el_.OwnerDocument.CreateElementWithText("FunctionName", m_FunctionName);
			node.AppendChild(valueNode);

			XmlElement argNode = (XmlElement)el_.OwnerDocument.CreateElement("ArgumentList");
			node.AppendChild(argNode);

			foreach (string a in m_Args)
			{
				valueNode = (XmlElement)el_.OwnerDocument.CreateElementWithText("Argument", a);
				argNode.AppendChild(valueNode);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public override void Load(XmlElement el_, SaveOption option_)
		{
			m_FunctionName = el_.SelectSingleNode("FunctionName").InnerText;
			List<string> args = new List<string>();

			foreach (XmlNode n in el_.SelectNodes("ArgumentList/Argument"))
			{
				args.Add(n.InnerText);
			}

			m_Args = args.ToArray();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.Function);
            bw_.Write(m_FunctionName);
            bw_.Write(m_Args.Length);

            foreach (string a in m_Args)
            {
                bw_.Write(a);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            br_.ReadInt32();
            m_FunctionName = br_.ReadString();
            int count = br_.ReadInt32();
            m_Args = new string[count];

            for (int i=0; i<count; i++)
            {
                m_Args[i] = br_.ReadString();
            }
        }

		#endregion

        #endregion
	}
}

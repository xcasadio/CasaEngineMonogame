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
    class CalculatorTokenKeyword
        : ICalculatorToken
    {

        string m_Keyword;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword_"></param>
        public CalculatorTokenKeyword(Calculator calculator_, string keyword_)
            : base(calculator_)
        {
            m_Keyword = keyword_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public CalculatorTokenKeyword(Calculator calculator_, XmlElement el_, SaveOption option_)
            : base(calculator_)
        {
            Load(el_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public CalculatorTokenKeyword(Calculator calculator_, BinaryReader br_, SaveOption option_)
            : base(calculator_)
        {
            Load(br_, option_);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override float Evaluate()
        {
            return this.Calculator.Parser.EvaluateKeyword(m_Keyword);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement node = (XmlElement)el_.OwnerDocument.CreateElement("Node");
            el_.AppendChild(node);
            el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Keyword).ToString());
            XmlElement valueNode = (XmlElement)el_.OwnerDocument.CreateElementWithText("Keyword", m_Keyword);
            node.AppendChild(valueNode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            m_Keyword = el_.SelectSingleNode("Keyword").InnerText;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.Keyword);
            bw_.Write(m_Keyword);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            br_.ReadInt32();
            m_Keyword = br_.ReadString();
        }


    }
}

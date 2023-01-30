using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenKeyword
        : ICalculatorToken
    {

        string m_Keyword;





        public CalculatorTokenKeyword(Calculator calculator_, string keyword_)
            : base(calculator_)
        {
            m_Keyword = keyword_;
        }

        public CalculatorTokenKeyword(Calculator calculator_, XmlElement el_, SaveOption option_)
            : base(calculator_)
        {
            Load(el_, option_);
        }

        public CalculatorTokenKeyword(Calculator calculator_, BinaryReader br_, SaveOption option_)
            : base(calculator_)
        {
            Load(br_, option_);
        }



        public override float Evaluate()
        {
            return this.Calculator.Parser.EvaluateKeyword(m_Keyword);
        }


        public override void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement node = (XmlElement)el_.OwnerDocument.CreateElement("Node");
            el_.AppendChild(node);
            el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Keyword).ToString());
            XmlElement valueNode = (XmlElement)el_.OwnerDocument.CreateElementWithText("Keyword", m_Keyword);
            node.AppendChild(valueNode);
        }

        public override void Load(XmlElement el_, SaveOption option_)
        {
            m_Keyword = el_.SelectSingleNode("Keyword").InnerText;
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.Keyword);
            bw_.Write(m_Keyword);
        }

        public override void Load(BinaryReader br_, SaveOption option_)
        {
            br_.ReadInt32();
            m_Keyword = br_.ReadString();
        }


    }
}

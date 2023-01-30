using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenFunction
        : ICalculatorToken
    {

        string m_FunctionName;
        string[] m_Args;





        public CalculatorTokenFunction(Calculator calculator_, string functionName_, string[] args_)
            : base(calculator_)
        {
            m_FunctionName = functionName_;
            m_Args = args_;
        }

        public CalculatorTokenFunction(Calculator calculator_, XmlElement el_, SaveOption option_)
            : base(calculator_)
        {
            Load(el_, option_);
        }

        public CalculatorTokenFunction(Calculator calculator_, BinaryReader br_, SaveOption option_)
            : base(calculator_)
        {
            Load(br_, option_);
        }



        public override float Evaluate()
        {
            return Calculator.Parser.EvaluateFunction(m_FunctionName, m_Args);
        }


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

        public override void Load(BinaryReader br_, SaveOption option_)
        {
            br_.ReadInt32();
            m_FunctionName = br_.ReadString();
            int count = br_.ReadInt32();
            m_Args = new string[count];

            for (int i = 0; i < count; i++)
            {
                m_Args[i] = br_.ReadString();
            }
        }


    }
}

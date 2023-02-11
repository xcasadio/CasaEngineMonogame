using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenFunction
        : CalculatorToken
    {

        string _functionName;
        string[] _args;





        public CalculatorTokenFunction(Calculator calculator, string functionName, string[] args)
            : base(calculator)
        {
            _functionName = functionName;
            _args = args;
        }

        public CalculatorTokenFunction(Calculator calculator, XmlElement el, SaveOption option)
            : base(calculator)
        {
            Load(el, option);
        }

        public CalculatorTokenFunction(Calculator calculator, BinaryReader br, SaveOption option)
            : base(calculator)
        {
            Load(br, option);
        }



        public override float Evaluate()
        {
            return Calculator.Parser.EvaluateFunction(_functionName, _args);
        }


        public override void Save(XmlElement el, SaveOption option)
        {
            XmlElement node = (XmlElement)el.OwnerDocument.CreateElement("Node");
            el.AppendChild(node);
            el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Function).ToString());
            XmlElement valueNode = (XmlElement)el.OwnerDocument.CreateElementWithText("FunctionName", _functionName);
            node.AppendChild(valueNode);

            XmlElement argNode = (XmlElement)el.OwnerDocument.CreateElement("ArgumentList");
            node.AppendChild(argNode);

            foreach (string a in _args)
            {
                valueNode = (XmlElement)el.OwnerDocument.CreateElementWithText("Argument", a);
                argNode.AppendChild(valueNode);
            }
        }

        public override void Load(XmlElement el, SaveOption option)
        {
            _functionName = el.SelectSingleNode("FunctionName").InnerText;
            List<string> args = new List<string>();

            foreach (XmlNode n in el.SelectNodes("ArgumentList/Argument"))
            {
                args.Add(n.InnerText);
            }

            _args = args.ToArray();
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write((int)CalculatorTokenType.Function);
            bw.Write(_functionName);
            bw.Write(_args.Length);

            foreach (string a in _args)
            {
                bw.Write(a);
            }
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            br.ReadInt32();
            _functionName = br.ReadString();
            int count = br.ReadInt32();
            _args = new string[count];

            for (int i = 0; i < count; i++)
            {
                _args[i] = br.ReadString();
            }
        }


    }
}

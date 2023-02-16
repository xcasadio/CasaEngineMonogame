using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Core.Parser
{
    class CalculatorTokenKeyword
        : CalculatorToken
    {

        string _keyword;





        public CalculatorTokenKeyword(Calculator calculator, string keyword)
            : base(calculator)
        {
            _keyword = keyword;
        }

        public CalculatorTokenKeyword(Calculator calculator, XmlElement el, SaveOption option)
            : base(calculator)
        {
            Load(el, option);
        }

        public CalculatorTokenKeyword(Calculator calculator, BinaryReader br, SaveOption option)
            : base(calculator)
        {
            Load(br, option);
        }



        public override float Evaluate()
        {
            return Calculator.Parser.EvaluateKeyword(_keyword);
        }


        public override void Save(XmlElement el, SaveOption option)
        {
            var node = (XmlElement)el.OwnerDocument.CreateElement("Node");
            el.AppendChild(node);
            el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Keyword).ToString());
            var valueNode = (XmlElement)el.OwnerDocument.CreateElementWithText("Keyword", _keyword);
            node.AppendChild(valueNode);
        }

        public override void Load(XmlElement el, SaveOption option)
        {
            _keyword = el.SelectSingleNode("Keyword").InnerText;
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write((int)CalculatorTokenType.Keyword);
            bw.Write(_keyword);
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            br.ReadInt32();
            _keyword = br.ReadString();
        }


    }
}

using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Parser
{
    internal class CalculatorTokenValue
        : CalculatorToken
    {
        private int _type;
        private float _value;
        private string _string;





        public CalculatorTokenValue(Calculator calculator, float value)
            : base(calculator)
        {
            _value = value;
            _type = 0;
        }

        public CalculatorTokenValue(Calculator calculator, string value)
            : base(calculator)
        {
            _string = value;
            _type = 1;
        }

        public CalculatorTokenValue(Calculator calculator, XmlElement el, SaveOption option)
            : base(calculator)
        {
            Load(el, option);
        }

        public CalculatorTokenValue(Calculator calculator, BinaryReader br, SaveOption option)
            : base(calculator)
        {
            Load(br, option);
        }



        public override float Evaluate()
        {
            return _value;
        }


        public override void Save(XmlElement el, SaveOption option)
        {
            var node = el.OwnerDocument.CreateElement("Node");
            el.AppendChild(node);
            el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Value).ToString());

            var value = _type == 0 ? _value.ToString() : _string;

            var valueNode = el.OwnerDocument.CreateElementWithText("Value", value);
            el.OwnerDocument.AddAttribute(valueNode, "type", _type.ToString());
            node.AppendChild(valueNode);
        }

        public override void Load(XmlElement el, SaveOption option)
        {
            _type = int.Parse(el.SelectSingleNode("Value").Attributes["type"].Value);
            if (_type == 0)
            {
                _value = float.Parse(el.SelectSingleNode("Value").InnerText);
            }
            else
            {
                _string = el.SelectSingleNode("Value").InnerText;
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write((int)CalculatorTokenType.Value);
            var value = _type == 0 ? _value.ToString() : _string;
            bw.Write(value);
            bw.Write(_type);
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            _type = br.ReadInt32();

            if (_type == 0)
            {
                _value = float.Parse(br.ReadString());
            }
            else
            {
                _string = br.ReadString();
            }
        }


    }
}

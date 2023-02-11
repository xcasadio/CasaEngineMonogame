using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenValue
        : CalculatorToken
    {

        int _type;
        float _value;
        string _string;





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
            XmlElement node = (XmlElement)el.OwnerDocument.CreateElement("Node");
            el.AppendChild(node);
            el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Value).ToString());

            string value = _type == 0 ? _value.ToString() : _string;

            XmlElement valueNode = (XmlElement)el.OwnerDocument.CreateElementWithText("Value", value);
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
            string value = _type == 0 ? _value.ToString() : _string;
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

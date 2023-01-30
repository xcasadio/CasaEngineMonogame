using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class Calculator
    {

        ICalculatorToken m_Root;
        readonly Parser m_Parser;



        public ICalculatorToken Root
        {
            get => m_Root;
            set => m_Root = value;
        }

        public CasaEngine.Design.Parser.Parser Parser => m_Parser;


        public Calculator(Parser parser_)
        {
            m_Parser = parser_;
        }



        public float Evaluate()
        {
            return m_Root.Evaluate();
        }


        public void Load(XmlElement el_, SaveOption option_)
        {
            m_Root = null;

            XmlElement root = (XmlElement)el_.SelectSingleNode("Root/Node");

            if (root != null)
            {
                m_Root = CreateCalculatorToken(this, root, option_);
            }
        }

        public void Save(XmlElement el_, SaveOption option_)
        {
            if (m_Root != null)
            {
                XmlNode node = el_.OwnerDocument.CreateElement("Root");
                el_.AppendChild(node);
                m_Root.Save((XmlElement)node, option_);
            }
        }

        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            if (m_Root != null)
            {
                m_Root.Save(bw_, option_);
            }
        }

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


    }
}

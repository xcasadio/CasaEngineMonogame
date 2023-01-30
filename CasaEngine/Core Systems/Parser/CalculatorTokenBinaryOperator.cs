using CasaEngineCommon.Extension;
using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenBinaryOperator
        : ICalculatorToken
    {
        public enum BinaryOperator
        {
            Plus,
            Minus,
            Multiply,
            Divide,

            Equal,
            Different,
            Superior,
            Inferior,
            Or,
            And,
            SupEqual,
            InfEqual,
        }


        BinaryOperator m_Operator;
        ICalculatorToken m_Left;
        ICalculatorToken m_Right;



        public ICalculatorToken Left
        {
            get => m_Left;
            set => m_Left = value;
        }

        public ICalculatorToken Right
        {
            get => m_Right;
            set => m_Right = value;
        }



        public CalculatorTokenBinaryOperator(Calculator calculator_, BinaryOperator operator_)
            : base(calculator_)
        {
            m_Operator = operator_;
        }

        public CalculatorTokenBinaryOperator(Calculator calculator_, XmlElement el_, SaveOption option_)
            : base(calculator_)
        {
            Load(el_, option_);
        }

        public CalculatorTokenBinaryOperator(Calculator calculator_, BinaryReader br_, SaveOption option_)
            : base(calculator_)
        {
            Load(br_, option_);
        }



        public override float Evaluate()
        {
            float res;

            switch (m_Operator)
            {
                case BinaryOperator.Plus:
                    res = m_Left.Evaluate() + m_Right.Evaluate();
                    break;

                case BinaryOperator.Minus:
                    res = m_Left.Evaluate() - m_Right.Evaluate();
                    break;

                case BinaryOperator.Multiply:
                    res = m_Left.Evaluate() * m_Right.Evaluate();
                    break;

                case BinaryOperator.Divide:
                    res = m_Left.Evaluate() / m_Right.Evaluate();
                    break;

                case BinaryOperator.Equal:
                    res = System.Convert.ToSingle(m_Left.Evaluate() == m_Right.Evaluate());
                    break;

                case BinaryOperator.Different:
                    res = System.Convert.ToSingle(m_Left.Evaluate() != m_Right.Evaluate());
                    break;

                case BinaryOperator.Superior:
                    res = System.Convert.ToSingle(m_Left.Evaluate() > m_Right.Evaluate());
                    break;

                case BinaryOperator.Inferior:
                    res = System.Convert.ToSingle(m_Left.Evaluate() < m_Right.Evaluate());
                    break;

                case BinaryOperator.SupEqual:
                    res = System.Convert.ToSingle(m_Left.Evaluate() >= m_Right.Evaluate());
                    break;

                case BinaryOperator.InfEqual:
                    res = System.Convert.ToSingle(m_Left.Evaluate() <= m_Right.Evaluate());
                    break;

                case BinaryOperator.Or:
                    res = System.Convert.ToSingle(System.Convert.ToBoolean(m_Left.Evaluate()) || System.Convert.ToBoolean(m_Right.Evaluate()));
                    break;

                case BinaryOperator.And:
                    res = System.Convert.ToSingle(System.Convert.ToBoolean(m_Left.Evaluate()) && System.Convert.ToBoolean(m_Right.Evaluate()));
                    break;

                default:
                    throw new InvalidOperationException("CalculatorTokenBinaryOperator.Evaluate() : BinaryOperator unknown");
            }

            return res;
        }


        public override void Save(XmlElement el_, SaveOption option_)
        {
            XmlElement node = (XmlElement)el_.OwnerDocument.CreateElement("Node");
            el_.AppendChild(node);
            el_.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.BinaryOperator).ToString());
            el_.OwnerDocument.AddAttribute(node, "operator", ((int)m_Operator).ToString());

            XmlElement child = (XmlElement)el_.OwnerDocument.CreateElement("Left");
            node.AppendChild(child);
            m_Left.Save(child, option_);

            child = (XmlElement)el_.OwnerDocument.CreateElement("Right");
            node.AppendChild(child);
            m_Right.Save(child, option_);
        }

        public override void Load(XmlElement el_, SaveOption option_)
        {
            m_Operator = (BinaryOperator)int.Parse(el_.Attributes["operator"].Value);

            XmlElement child = (XmlElement)el_.SelectSingleNode("Left/Node");
            m_Left = Calculator.CreateCalculatorToken(Calculator, child, option_);

            child = (XmlElement)el_.SelectSingleNode("Right/Node");
            m_Right = Calculator.CreateCalculatorToken(Calculator, child, option_);

        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write((int)CalculatorTokenType.BinaryOperator);
            bw_.Write((int)m_Operator);
            m_Left.Save(bw_, option_);
            m_Right.Save(bw_, option_);
        }

        public override void Load(BinaryReader br_, SaveOption option_)
        {
            m_Operator = (BinaryOperator)br_.ReadInt32();
            m_Left = Calculator.CreateCalculatorToken(Calculator, br_, option_);
            m_Right = Calculator.CreateCalculatorToken(Calculator, br_, option_);
        }



    }
}

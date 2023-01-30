using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    enum CalculatorTokenType
    {
        BinaryOperator,
        UnaryOperator,
        Keyword,
        Value,
        Function
    }

    abstract class ICalculatorToken
        : ISaveLoad
    {
        readonly Calculator m_Calculator;



        public CasaEngine.Design.Parser.Calculator Calculator => m_Calculator;


        protected ICalculatorToken(Calculator calculator_)
        {
            m_Calculator = calculator_;
        }



        public abstract float Evaluate();

        protected float EvaluateKeyword(string keyword_)
        {
            return m_Calculator.Parser.EvaluateKeyword(keyword_);
        }


        public abstract void Save(XmlElement el_, SaveOption option_);
        public abstract void Load(XmlElement el_, SaveOption option_);

        public abstract void Save(BinaryWriter bw_, SaveOption option_);
        public abstract void Load(BinaryReader br_, SaveOption option_);


    }
}

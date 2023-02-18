using System.Xml;
using CasaEngine.Core.Design;

namespace CasaEngine.Core.Parser
{
    enum CalculatorTokenType
    {
        BinaryOperator,
        UnaryOperator,
        Keyword,
        Value,
        Function
    }

    abstract class CalculatorToken
        : ISaveLoad
    {
        readonly Calculator _calculator;



        public Calculator Calculator => _calculator;


        protected CalculatorToken(Calculator calculator)
        {
            _calculator = calculator;
        }



        public abstract float Evaluate();

        protected float EvaluateKeyword(string keyword)
        {
            return _calculator.Parser.EvaluateKeyword(keyword);
        }


        public abstract void Save(XmlElement el, SaveOption option);
        public abstract void Load(XmlElement el, SaveOption option);

        public abstract void Save(BinaryWriter bw, SaveOption option);
        public abstract void Load(BinaryReader br, SaveOption option);


    }
}

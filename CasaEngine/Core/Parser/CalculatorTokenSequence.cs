using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Core.Parser
{
    class CalculatorTokenSequence
        : CalculatorToken
    {
        public enum TokenSequence
        {
            Sequence,
            StartSequence,
            EndSequence
        }


        readonly TokenSequence _sequence;



        public TokenSequence Sequence => _sequence;


        public CalculatorTokenSequence(Calculator calculator, TokenSequence sequence)
            : base(calculator)
        {
            _sequence = sequence;
        }



        public override float Evaluate()
        {
            throw new InvalidOperationException("Don't use to evaluate");
        }


        public override void Save(XmlElement el, SaveOption option)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        public override void Load(XmlElement el, SaveOption option)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary objecte");
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }


    }
}

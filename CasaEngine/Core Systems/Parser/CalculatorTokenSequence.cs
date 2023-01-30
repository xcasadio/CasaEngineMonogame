using System.Xml;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    class CalculatorTokenSequence
        : ICalculatorToken
    {
        public enum TokenSequence
        {
            Sequence,
            StartSequence,
            EndSequence
        }


        readonly TokenSequence m_Sequence;



        public TokenSequence Sequence => m_Sequence;


        public CalculatorTokenSequence(Calculator calculator_, TokenSequence sequence_)
            : base(calculator_)
        {
            m_Sequence = sequence_;
        }



        public override float Evaluate()
        {
            throw new InvalidOperationException("Don't use to evaluate");
        }


        public override void Save(XmlElement el_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        public override void Load(XmlElement el_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary objecte");
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        public override void Load(BinaryReader br_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }


    }
}

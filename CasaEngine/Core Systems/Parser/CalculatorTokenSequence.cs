using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// 
    /// </summary>
    class CalculatorTokenSequence
        : ICalculatorToken
    {
        /// <summary>
        /// 
        /// </summary>
        public enum TokenSequence
        {
            Sequence,
            StartSequence,
            EndSequence
        }


        TokenSequence m_Sequence;



        /// <summary>
        /// 
        /// </summary>
        public TokenSequence Sequence
        {
            get { return m_Sequence; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence_"></param>
        public CalculatorTokenSequence(Calculator calculator_, TokenSequence sequence_)
            : base(calculator_)
        {
            m_Sequence = sequence_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override float Evaluate()
        {
            throw new InvalidOperationException("Don't use to evaluate");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary objecte");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            throw new InvalidOperationException("Can't save this object. It is a temporary object");
        }


    }
}

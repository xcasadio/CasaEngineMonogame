using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
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

    /// <summary>
    /// 
    /// </summary>
    abstract class ICalculatorToken
        : ISaveLoad
    {

        Calculator m_Calculator;



        /// <summary>
        /// 
        /// </summary>
        public CasaEngine.Design.Parser.Calculator Calculator
        {
            get { return m_Calculator; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="calculator_"></param>
        protected ICalculatorToken(Calculator calculator_)
        {
            m_Calculator = calculator_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract float Evaluate();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword_"></param>
        /// <returns></returns>
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

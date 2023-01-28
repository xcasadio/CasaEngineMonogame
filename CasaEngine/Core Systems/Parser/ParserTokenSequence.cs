using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// 
    /// </summary>
    class ParserTokenSequence
        : ParserToken
    {

        static public readonly string sequence = "`";





        /// <summary>
        /// 
        /// </summary>
        /// <param name="parser_"></param>
        public ParserTokenSequence(Parser parser_)
            : base(parser_, sequence)
        {

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public override bool Check(string sentence_)
        {
            if (m_Token.Equals(sentence_) == true)
            {
                Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.Sequence));
                return true;
            }

            return false;
        }

    }
}

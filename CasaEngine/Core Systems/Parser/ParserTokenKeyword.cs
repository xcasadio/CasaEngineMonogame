using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// 
    /// </summary>
    class ParserTokenKeyword
        : ParserToken
    {





        /// <summary>
        /// 
        /// </summary>
        /// <param name="parser_"></param>
        /// <param name="token_"></param>
        public ParserTokenKeyword(Parser parser_, string token_)
            : base(parser_, token_)
        { }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public override bool Check(string sentence_)
        {
            if (m_Token.ToLower().Equals(sentence_.ToLower()) == true)
            {
                Parser.AddCalculator(new CalculatorTokenKeyword(Parser.Calculator, m_Token));
                return true;
            }

            return false;
        }

    }
}

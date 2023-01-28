using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Design.Parser
{
    /// <summary>
    /// Mother for all token than the Parser can identify
    /// </summary>
	abstract class ParserToken
    {

        protected string m_Token = string.Empty;
        Parser m_Parser;



        /// <summary>
        /// Gets
        /// </summary>
        protected Parser Parser
        {
            get { return m_Parser; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="parser_"></param>
        /// <param name="token_"></param>
        protected ParserToken(Parser parser_, string token_)
        {
            parser_.AddToken(token_);

            m_Token = token_;
            m_Parser = parser_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentence_"></param>
        /// <returns></returns>
        public abstract bool Check(string sentence_);

    }
}

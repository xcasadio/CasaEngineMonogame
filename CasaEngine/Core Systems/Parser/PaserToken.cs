namespace CasaEngine.Design.Parser
{
    abstract class ParserToken
    {

        protected string m_Token = string.Empty;
        readonly Parser m_Parser;



        protected Parser Parser => m_Parser;


        protected ParserToken(Parser parser_, string token_)
        {
            parser_.AddToken(token_);

            m_Token = token_;
            m_Parser = parser_;
        }



        public abstract bool Check(string sentence_);

    }
}

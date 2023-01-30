namespace CasaEngine.Design.Parser
{
    class ParserTokenUnaryOperator
        : ParserToken
    {





        public ParserTokenUnaryOperator(Parser parser_, string token_)
            : base(parser_, token_)
        {

        }



        public override bool Check(string sentence_)
        {
            if (sentence_.StartsWith(m_Token) == true)
            {
                Parser.Check(m_Token.Substring(1));
            }

            return false;
        }

    }
}

namespace CasaEngine.Design.Parser
{
    class ParserTokenVariable
        : ParserToken
    {





        public ParserTokenVariable(Parser parser_)
            : base(parser_, string.Empty)
        {

        }



        public override bool Check(string sentence_)
        {
            if (string.IsNullOrEmpty(sentence_) == true)
            {
                return true;
            }

            int c = (int)sentence_.ToCharArray(0, 1)[0];

            if (c >= (int)'0' || c <= (int)'9')
            {
                return false;
            }

            m_Token = sentence_;

            return true;
        }

    }
}

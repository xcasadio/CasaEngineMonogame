namespace CasaEngine.Design.Parser
{
    class ParserTokenBinaryOperator
        : ParserToken
    {





        public ParserTokenBinaryOperator(Parser parser_, string token_)
            : base(parser_, token_)
        {

        }



        public override bool Check(string sentence_)
        {
            int index = sentence_.IndexOf(m_Token);

            if (index != -1)
            {
                Parser.AddCalculator(Parser.GetCalculatorByBinaryOperator(m_Token));

                string s1, s2;
                s1 = sentence_.Substring(0, index);
                s2 = sentence_.Substring(index + m_Token.Length, sentence_.Length - index - m_Token.Length);

                return Parser.Check(s1) && Parser.Check(s2);
            }

            return false;
        }

    }
}

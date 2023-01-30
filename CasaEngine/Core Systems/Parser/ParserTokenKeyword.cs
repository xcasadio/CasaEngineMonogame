namespace CasaEngine.Design.Parser
{
    class ParserTokenKeyword
        : ParserToken
    {





        public ParserTokenKeyword(Parser parser_, string token_)
            : base(parser_, token_)
        { }



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

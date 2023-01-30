namespace CasaEngine.Design.Parser
{
    class ParserTokenSequence
        : ParserToken
    {

        static public readonly string sequence = "`";





        public ParserTokenSequence(Parser parser_)
            : base(parser_, sequence)
        {

        }



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

namespace CasaEngine.Design.Parser
{
    class ParserTokenSequence
        : ParserToken
    {

        static public readonly string Sequence = "`";





        public ParserTokenSequence(Parser parser)
            : base(parser, Sequence)
        {

        }



        public override bool Check(string sentence)
        {
            if (Token.Equals(sentence) == true)
            {
                Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.Sequence));
                return true;
            }

            return false;
        }

    }
}

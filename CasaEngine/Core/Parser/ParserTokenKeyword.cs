namespace CasaEngine.Core.Parser
{
    internal class ParserTokenKeyword
        : ParserToken
    {





        public ParserTokenKeyword(Parser parser, string token)
            : base(parser, token)
        { }



        public override bool Check(string sentence)
        {
            if (Token.ToLower().Equals(sentence.ToLower()))
            {
                Parser.AddCalculator(new CalculatorTokenKeyword(Parser.Calculator, Token));
                return true;
            }

            return false;
        }

    }
}

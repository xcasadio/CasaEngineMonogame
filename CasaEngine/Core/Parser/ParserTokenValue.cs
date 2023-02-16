namespace CasaEngine.Core.Parser
{
    class ParserTokenValue
        : ParserToken
    {

        float _value;





        public ParserTokenValue(Parser parser)
            : base(parser, string.Empty)
        {

        }



        public override bool Check(string sentence)
        {
            //int value;

            if (string.IsNullOrEmpty(sentence))
            {
                return false;
            }

            if (float.TryParse(sentence, out _value))
            {
                Parser.AddCalculator(new CalculatorTokenValue(Parser.Calculator, _value));
            }
            else
            {
                Parser.AddCalculator(new CalculatorTokenValue(Parser.Calculator, sentence));
            }

            Token = sentence;


            return true;
        }

    }
}

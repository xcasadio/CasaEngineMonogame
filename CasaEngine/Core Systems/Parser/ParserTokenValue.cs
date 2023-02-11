namespace CasaEngine.Design.Parser
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

            if (string.IsNullOrEmpty(sentence) == true)
            {
                return false;
            }

            if (float.TryParse(sentence, out _value) == true)
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

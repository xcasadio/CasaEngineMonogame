namespace CasaEngine.Core_Systems.Parser
{
    class ParserTokenVariable
        : ParserToken
    {





        public ParserTokenVariable(Parser parser)
            : base(parser, string.Empty)
        {

        }



        public override bool Check(string sentence)
        {
            if (string.IsNullOrEmpty(sentence))
            {
                return true;
            }

            var c = (int)sentence.ToCharArray(0, 1)[0];

            if (c >= (int)'0' || c <= (int)'9')
            {
                return false;
            }

            Token = sentence;

            return true;
        }

    }
}

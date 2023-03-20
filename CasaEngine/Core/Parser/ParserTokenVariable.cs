namespace CasaEngine.Core.Parser
{
    internal class ParserTokenVariable
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

            if (c >= '0' || c <= '9')
            {
                return false;
            }

            Token = sentence;

            return true;
        }

    }
}

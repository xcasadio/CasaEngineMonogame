namespace CasaEngine.Design.Parser
{
    abstract class ParserToken
    {

        protected string Token = string.Empty;
        readonly Parser _parser;



        protected Parser Parser => _parser;


        protected ParserToken(Parser parser, string token)
        {
            parser.AddToken(token);

            Token = token;
            _parser = parser;
        }



        public abstract bool Check(string sentence);

    }
}

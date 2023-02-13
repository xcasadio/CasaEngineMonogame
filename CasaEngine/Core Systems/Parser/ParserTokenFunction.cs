namespace CasaEngine.Core_Systems.Parser
{
    class ParserTokenFunction
        : ParserToken
    {





        public ParserTokenFunction(Parser parser, string token)
            : base(parser, token)
        { }



        public override bool Check(string sentence)
        {
            if (sentence.StartsWith(Token.ToLower()))
            {
                var args = new List<string>();
                var str = sentence.Replace(Token + "=", "");
                var s2 = string.Empty;

                if (str.StartsWith("\""))
                {
                    str = str.Substring(1);

                    var index = str.IndexOf("\"");

                    if (index != -1)
                    {
                        var argument = str.Substring(0, index);
                        var a = argument.Split(',');

                        s2 = str.Substring(index + 1, str.Length - index - 1);

                        foreach (var s in a)
                        {
                            args.Add(s);
                        }
                    }
                    else
                    {
                        //error
                        return false;
                    }
                }

                Parser.AddCalculator(new CalculatorTokenFunction(Parser.Calculator, Token, args.ToArray()));

                /*if (string.IsNullOrEmpty(s2) == false)
				{
					return Parser.Check(s2);
				}
				else
				{
					return true;
				}*/

                return true;
            }

            return false;
        }

    }
}

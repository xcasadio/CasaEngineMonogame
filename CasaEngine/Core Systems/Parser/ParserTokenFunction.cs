namespace CasaEngine.Design.Parser
{
    class ParserTokenFunction
        : ParserToken
    {





        public ParserTokenFunction(Parser parser, string token)
            : base(parser, token)
        { }



        public override bool Check(string sentence)
        {
            if (sentence.StartsWith(Token.ToLower()) == true)
            {
                List<string> args = new List<string>();
                string str = sentence.Replace(Token + "=", "");
                string s2 = string.Empty;

                if (str.StartsWith("\"") == true)
                {
                    str = str.Substring(1);

                    int index = str.IndexOf("\"");

                    if (index != -1)
                    {
                        string argument = str.Substring(0, index);
                        string[] a = argument.Split(',');

                        s2 = str.Substring(index + 1, str.Length - index - 1);

                        foreach (string s in a)
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

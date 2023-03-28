namespace CasaEngine.Core.Parser;

internal class ParserTokenBinaryOperator
    : ParserToken
{





    public ParserTokenBinaryOperator(Parser parser, string token)
        : base(parser, token)
    {

    }



    public override bool Check(string sentence)
    {
        var index = sentence.IndexOf(Token);

        if (index != -1)
        {
            Parser.AddCalculator(Parser.GetCalculatorByBinaryOperator(Token));

            string s1, s2;
            s1 = sentence.Substring(0, index);
            s2 = sentence.Substring(index + Token.Length, sentence.Length - index - Token.Length);

            return Parser.Check(s1) && Parser.Check(s2);
        }

        return false;
    }

}
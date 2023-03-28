namespace CasaEngine.Core.Parser;

internal class ParserTokenDelimiter
    : ParserToken
{
    private readonly string _close;





    public ParserTokenDelimiter(Parser parser, string open, string close)
        : base(parser, open)
    {
        _close = close;
        Parser.AddToken(close);
    }



    public override bool Check(string sentence)
    {
        string res;
        string outside;

        if (GetStringBetweenDelimiter(sentence, Token, _close, out res, out outside))
        {
            var r = true;

            //attention inverse droite et gauche !!!!!!
            if (string.IsNullOrEmpty(outside) == false)
            {
                r = Parser.Check(outside);
            }

            Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.StartSequence));
            r &= Parser.Check(res);
            Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.EndSequence));

            return r;
        }

        return false;
    }

    public static bool GetStringBetweenDelimiter(string str, string open, string close, out string s1, out string s2)
    {
        s1 = string.Empty;
        s2 = string.Empty;

        var first = str.IndexOf(open);

        if (first != -1)
        {
            var p = 1; // one open found
            var index = -1;

            for (var i = first + open.Length; i < str.Length; i += open.Length)
            {
                var tmp = str.Substring(i, str.Length - open.Length - i + 1);

                if (tmp.StartsWith(open))
                {
                    p++;
                }
                else if (tmp.StartsWith(close))
                {
                    p--;
                }

                if (p == 0)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1 || index == first + open.Length)
            {
                return false;
            }

            s1 = str.Substring(first + open.Length, index - open.Length - first).Trim();
            s2 = str.Substring(open.Length + index, str.Length - index - 1).Trim();
            //on decoupe a gauche puis a droite
            s2 = str.Substring(0, first + open.Length - 1).Trim();
            s2 += ParserTokenSequence.Sequence;
            s2 += str.Substring(open.Length + index, str.Length - index - 1).Trim();

            return true;
        }

        return false;
    }

}
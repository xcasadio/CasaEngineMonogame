namespace CasaEngine.Core.Parser;

internal class ParserTokenSequence
    : ParserToken
{

    public static readonly string Sequence = "`";





    public ParserTokenSequence(Parser parser)
        : base(parser, Sequence)
    {

    }



    public override bool Check(string sentence)
    {
        if (Token.Equals(sentence))
        {
            Parser.AddCalculator(new CalculatorTokenSequence(Parser.Calculator, CalculatorTokenSequence.TokenSequence.Sequence));
            return true;
        }

        return false;
    }

}
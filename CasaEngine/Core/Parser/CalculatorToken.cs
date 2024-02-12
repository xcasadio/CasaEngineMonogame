
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal enum CalculatorTokenType
{
    BinaryOperator,
    UnaryOperator,
    Keyword,
    Value,
    Function
}

internal abstract class CalculatorToken
{
    private readonly Calculator _calculator;

    public Calculator Calculator => _calculator;

    protected CalculatorToken(Calculator calculator)
    {
        _calculator = calculator;
    }

    public abstract float Evaluate();

    protected float EvaluateKeyword(string keyword)
    {
        return _calculator.Parser.EvaluateKeyword(keyword);
    }

    public abstract void Load(JObject element);

#if EDITOR
    public abstract void Save(JObject jObject);
#endif
}
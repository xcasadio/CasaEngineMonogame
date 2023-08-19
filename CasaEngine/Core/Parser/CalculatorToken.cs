using System.Text.Json;
using CasaEngine.Core.Design;
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

internal abstract class CalculatorToken : ISaveLoad
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

    public abstract void Load(JsonElement element, SaveOption option);

#if EDITOR
    public abstract void Save(JObject jObject, SaveOption option);
#endif
}
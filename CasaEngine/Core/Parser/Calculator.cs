using System.Text.Json;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class Calculator
{
    private CalculatorToken _root;
    private readonly Parser _parser;

    public CalculatorToken Root
    {
        get => _root;
        set => _root = value;
    }

    public Parser Parser => _parser;

    public Calculator(Parser parser)
    {
        _parser = parser;
    }

    public float Evaluate()
    {
        return _root.Evaluate();
    }

    public void Load(JsonElement element)
    {
        _root = CreateCalculatorToken(this, element.GetProperty("root"));
    }

    public static CalculatorToken CreateCalculatorToken(Calculator calculator, JsonElement element)
    {
        CalculatorToken token = null;

        var type = element.GetProperty("type").GetEnum<CalculatorTokenType>();

        switch (type)
        {
            case CalculatorTokenType.BinaryOperator:
                token = new CalculatorTokenBinaryOperator(calculator, element);
                break;

            case CalculatorTokenType.Keyword:
                token = new CalculatorTokenKeyword(calculator, element);
                break;

            /*case CalculatorTokenType.UnaryOperator:
                token = new CalculatorTokenUnaryOperator(el__);
                break;*/

            case CalculatorTokenType.Value:
                token = new CalculatorTokenValue(calculator, element);
                break;

            case CalculatorTokenType.Function:
                token = new CalculatorTokenFunction(calculator, element);
                break;

            default:
                throw new InvalidOperationException("unknown CalculatorTokenType");
        }

        return token;
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        if (_root != null)
        {
            var rootObject = new JObject();
            _root.Save(rootObject);
            jObject.Add("root", rootObject);
        }
    }

#endif
}
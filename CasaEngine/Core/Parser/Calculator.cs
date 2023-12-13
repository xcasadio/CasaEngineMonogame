using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
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

    public void Load(JsonElement element, SaveOption option)
    {
        _root = CreateCalculatorToken(this, element.GetProperty("root"), option);
    }

    public static CalculatorToken CreateCalculatorToken(Calculator calculator, JsonElement element, SaveOption option)
    {
        CalculatorToken token = null;

        var type = element.GetProperty("type").GetEnum<CalculatorTokenType>();

        switch (type)
        {
            case CalculatorTokenType.BinaryOperator:
                token = new CalculatorTokenBinaryOperator(calculator, element, option);
                break;

            case CalculatorTokenType.Keyword:
                token = new CalculatorTokenKeyword(calculator, element, option);
                break;

            /*case CalculatorTokenType.UnaryOperator:
                token = new CalculatorTokenUnaryOperator(el_, option_);
                break;*/

            case CalculatorTokenType.Value:
                token = new CalculatorTokenValue(calculator, element, option);
                break;

            case CalculatorTokenType.Function:
                token = new CalculatorTokenFunction(calculator, element, option);
                break;

            default:
                throw new InvalidOperationException("unknown CalculatorTokenType");
        }

        return token;
    }

#if EDITOR

    public void Save(JObject jObject, SaveOption option)
    {
        if (_root != null)
        {
            var rootObject = new JObject();
            _root.Save(rootObject, option);
            jObject.Add("root", rootObject);
        }
    }

#endif
}
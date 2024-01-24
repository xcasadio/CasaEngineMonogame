using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenFunction : CalculatorToken
{
    private string _functionName;
    private string[] _args;

    public CalculatorTokenFunction(Calculator calculator, string functionName, string[] args)
        : base(calculator)
    {
        _functionName = functionName;
        _args = args;
    }

    public CalculatorTokenFunction(Calculator calculator, JsonElement element, SaveOption option)
        : base(calculator)
    {
        Load(element, option);
    }

    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateFunction(_functionName, _args);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        _functionName = element.GetProperty("FunctionName").GetString();
        var args = new List<string>();

        foreach (var arrayElement in element.GetProperty("arguments").EnumerateArray())
        {
            args.Add(arrayElement.GetString());
        }

        _args = args.ToArray();
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("type", CalculatorTokenType.Value.ConvertToString());
        jObject.Add("function_name", _functionName);

        var argumentList = new JArray();
        foreach (var argument in _args)
        {
            argumentList.Add(argument);
        }
        jObject.Add("arguments", argumentList);
    }

#endif
}
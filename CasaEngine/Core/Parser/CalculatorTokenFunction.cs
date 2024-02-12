
using CasaEngine.Core.Serialization;
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

    public CalculatorTokenFunction(Calculator calculator, JObject element)
        : base(calculator)
    {
        Load(element);
    }

    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateFunction(_functionName, _args);
    }

    public override void Load(JObject element)
    {
        _functionName = element["FunctionName"].GetString();
        var args = new List<string>();

        foreach (var arrayElement in element["arguments"])
        {
            args.Add(arrayElement.GetString());
        }

        _args = args.ToArray();
    }

#if EDITOR

    public override void Save(JObject jObject)
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
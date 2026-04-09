
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parsing;

internal class CalculatorTokenKeyword : CalculatorToken
{
    private string _keyword;

    public CalculatorTokenKeyword(Calculator calculator, string keyword)
        : base(calculator)
    {
        _keyword = keyword;
    }

    public CalculatorTokenKeyword(Calculator calculator, JObject element)
        : base(calculator)
    {
        Load(element);
    }

    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateKeyword(_keyword);
    }

    public override void Load(JObject element)
    {
        _keyword = element["keyword"].GetString();
    }

}
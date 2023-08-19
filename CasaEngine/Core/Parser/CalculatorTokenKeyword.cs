using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenKeyword : CalculatorToken
{
    private string _keyword;

    public CalculatorTokenKeyword(Calculator calculator, string keyword)
        : base(calculator)
    {
        _keyword = keyword;
    }

    public CalculatorTokenKeyword(Calculator calculator, JsonElement element, SaveOption option)
        : base(calculator)
    {
        Load(element, option);
    }

    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateKeyword(_keyword);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        _keyword = element.GetProperty("keyword").GetString();
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("type", CalculatorTokenType.Keyword.ConvertToString());
        jObject.Add("keyword", _keyword);
    }

#endif
}
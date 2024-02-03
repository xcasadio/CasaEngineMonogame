using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
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

    public CalculatorTokenKeyword(Calculator calculator, JsonElement element)
        : base(calculator)
    {
        Load(element);
    }

    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateKeyword(_keyword);
    }

    public override void Load(JsonElement element)
    {
        _keyword = element.GetProperty("keyword").GetString();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        jObject.Add("type", CalculatorTokenType.Keyword.ConvertToString());
        jObject.Add("keyword", _keyword);
    }

#endif
}
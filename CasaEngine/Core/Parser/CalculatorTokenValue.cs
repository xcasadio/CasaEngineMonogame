
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenValue : CalculatorToken
{
    private int _type;
    private float _value;
    private string _string;

    public CalculatorTokenValue(Calculator calculator, float value)
        : base(calculator)
    {
        _value = value;
        _type = 0;
    }

    public CalculatorTokenValue(Calculator calculator, string value)
        : base(calculator)
    {
        _string = value;
        _type = 1;
    }

    public CalculatorTokenValue(Calculator calculator, JObject element)
        : base(calculator)
    {
        Load(element);
    }

    public override float Evaluate()
    {
        return _value;
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        jObject.Add("type", CalculatorTokenType.Value.ConvertToString());
        jObject.Add("value", _type == 0 ? _value : _string);
    }

#endif

    public override void Load(JObject element)
    {
        _type = (int)element["type"].GetEnum<CalculatorTokenType>();

        if (_type == 0)
        {
            _value = element["value"].GetSingle();
        }
        else
        {
            _string = element["value"].GetString();
        }
    }
}
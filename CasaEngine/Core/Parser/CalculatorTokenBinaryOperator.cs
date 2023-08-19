using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenBinaryOperator : CalculatorToken
{
    public enum BinaryOperator
    {
        Plus,
        Minus,
        Multiply,
        Divide,

        Equal,
        Different,
        Superior,
        Inferior,
        Or,
        And,
        SupEqual,
        InfEqual,
    }

    private BinaryOperator _operator;
    private CalculatorToken _left;
    private CalculatorToken _right;

    public CalculatorToken Left
    {
        get => _left;
        set => _left = value;
    }

    public CalculatorToken Right
    {
        get => _right;
        set => _right = value;
    }

    public CalculatorTokenBinaryOperator(Calculator calculator, BinaryOperator @operator)
        : base(calculator)
    {
        _operator = @operator;
    }

    public CalculatorTokenBinaryOperator(Calculator calculator, JsonElement element, SaveOption option)
        : base(calculator)
    {
        Load(element, option);
    }

    public override float Evaluate()
    {
        float res;

        switch (_operator)
        {
            case BinaryOperator.Plus:
                res = _left.Evaluate() + _right.Evaluate();
                break;

            case BinaryOperator.Minus:
                res = _left.Evaluate() - _right.Evaluate();
                break;

            case BinaryOperator.Multiply:
                res = _left.Evaluate() * _right.Evaluate();
                break;

            case BinaryOperator.Divide:
                res = _left.Evaluate() / _right.Evaluate();
                break;

            case BinaryOperator.Equal:
                res = Convert.ToSingle(_left.Evaluate() == _right.Evaluate());
                break;

            case BinaryOperator.Different:
                res = Convert.ToSingle(_left.Evaluate() != _right.Evaluate());
                break;

            case BinaryOperator.Superior:
                res = Convert.ToSingle(_left.Evaluate() > _right.Evaluate());
                break;

            case BinaryOperator.Inferior:
                res = Convert.ToSingle(_left.Evaluate() < _right.Evaluate());
                break;

            case BinaryOperator.SupEqual:
                res = Convert.ToSingle(_left.Evaluate() >= _right.Evaluate());
                break;

            case BinaryOperator.InfEqual:
                res = Convert.ToSingle(_left.Evaluate() <= _right.Evaluate());
                break;

            case BinaryOperator.Or:
                res = Convert.ToSingle(Convert.ToBoolean(_left.Evaluate()) || Convert.ToBoolean(_right.Evaluate()));
                break;

            case BinaryOperator.And:
                res = Convert.ToSingle(Convert.ToBoolean(_left.Evaluate()) && Convert.ToBoolean(_right.Evaluate()));
                break;

            default:
                throw new InvalidOperationException("CalculatorTokenBinaryOperator.Evaluate() : BinaryOperator unknown");
        }

        return res;
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("type", ((int)CalculatorTokenType.BinaryOperator).ToString());
        jObject.Add("operator", ((int)_operator).ToString());

        var leftElement = new JObject();
        _left.Save(leftElement, option);
        jObject.Add("left", leftElement);

        var rightElement = new JObject();
        _right.Save(rightElement, option);
        jObject.Add("right", rightElement);
    }
#endif

    public override void Load(JsonElement element, SaveOption option)
    {
        _operator = element.GetProperty("operator").GetEnum<BinaryOperator>();
        _left = Calculator.CreateCalculatorToken(Calculator, element.GetProperty("left"), option);
        _right = Calculator.CreateCalculatorToken(Calculator, element.GetProperty("right"), option);

    }
}
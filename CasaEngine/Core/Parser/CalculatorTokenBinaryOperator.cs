using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenBinaryOperator
    : CalculatorToken
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

    public CalculatorTokenBinaryOperator(Calculator calculator, XmlElement el, SaveOption option)
        : base(calculator)
    {
        Load(el, option);
    }

    public CalculatorTokenBinaryOperator(Calculator calculator, BinaryReader br, SaveOption option)
        : base(calculator)
    {
        Load(br, option);
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


    public override void Save(XmlElement el, SaveOption option)
    {
        var node = el.OwnerDocument.CreateElement("Node");
        el.AppendChild(node);
        el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.BinaryOperator).ToString());
        el.OwnerDocument.AddAttribute(node, "operator", ((int)_operator).ToString());

        var child = el.OwnerDocument.CreateElement("Left");
        node.AppendChild(child);
        _left.Save(child, option);

        child = el.OwnerDocument.CreateElement("Right");
        node.AppendChild(child);
        _right.Save(child, option);
    }

    public override void Load(XmlElement el, SaveOption option)
    {
        _operator = (BinaryOperator)int.Parse(el.Attributes["operator"].Value);

        var child = (XmlElement)el.SelectSingleNode("Left/Node");
        _left = Calculator.CreateCalculatorToken(Calculator, child, option);

        child = (XmlElement)el.SelectSingleNode("Right/Node");
        _right = Calculator.CreateCalculatorToken(Calculator, child, option);

    }

    public override void Save(BinaryWriter bw, SaveOption option)
    {
        bw.Write((int)CalculatorTokenType.BinaryOperator);
        bw.Write((int)_operator);
        _left.Save(bw, option);
        _right.Save(bw, option);
    }

    public override void Load(BinaryReader br, SaveOption option)
    {
        _operator = (BinaryOperator)br.ReadInt32();
        _left = Calculator.CreateCalculatorToken(Calculator, br, option);
        _right = Calculator.CreateCalculatorToken(Calculator, br, option);
    }



}
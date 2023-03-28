using System.Xml;
using CasaEngine.Core.Design;

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


    public void Load(XmlElement el, SaveOption option)
    {
        _root = null;

        var root = (XmlElement)el.SelectSingleNode("Root/Node");

        if (root != null)
        {
            _root = CreateCalculatorToken(this, root, option);
        }
    }

    public void Save(XmlElement el, SaveOption option)
    {
        if (_root != null)
        {
            XmlNode node = el.OwnerDocument.CreateElement("Root");
            el.AppendChild(node);
            _root.Save((XmlElement)node, option);
        }
    }

    public void Save(BinaryWriter bw, SaveOption option)
    {
        if (_root != null)
        {
            _root.Save(bw, option);
        }
    }

    public static CalculatorToken CreateCalculatorToken(Calculator calculator, XmlElement el, SaveOption option)
    {
        CalculatorToken token = null;

        var type = (CalculatorTokenType)int.Parse(el.Attributes["type"].Value);

        switch (type)
        {
            case CalculatorTokenType.BinaryOperator:
                token = new CalculatorTokenBinaryOperator(calculator, el, option);
                break;

            case CalculatorTokenType.Keyword:
                token = new CalculatorTokenKeyword(calculator, el, option);
                break;

            /*case CalculatorTokenType.UnaryOperator:
                token = new CalculatorTokenUnaryOperator(el_, option_);
                break;*/

            case CalculatorTokenType.Value:
                token = new CalculatorTokenValue(calculator, el, option);
                break;

            case CalculatorTokenType.Function:
                token = new CalculatorTokenFunction(calculator, el, option);
                break;

            default:
                throw new InvalidOperationException("unknown CalculatorTokenType");
        }

        return token;
    }

    public static CalculatorToken CreateCalculatorToken(Calculator calculator, BinaryReader br, SaveOption option)
    {
        CalculatorToken token = null;

        var type = (CalculatorTokenType)br.ReadInt32();

        switch (type)
        {
            case CalculatorTokenType.BinaryOperator:
                token = new CalculatorTokenBinaryOperator(calculator, br, option);
                break;

            case CalculatorTokenType.Keyword:
                token = new CalculatorTokenKeyword(calculator, br, option);
                break;

            /*case CalculatorTokenType.UnaryOperator:
                token = new CalculatorTokenUnaryOperator(el_, option_);
                break;*/

            case CalculatorTokenType.Value:
                token = new CalculatorTokenValue(calculator, br, option);
                break;

            case CalculatorTokenType.Function:
                token = new CalculatorTokenFunction(calculator, br, option);
                break;

            default:
                throw new InvalidOperationException("unknown CalculatorTokenType");
        }

        return token;
    }


}
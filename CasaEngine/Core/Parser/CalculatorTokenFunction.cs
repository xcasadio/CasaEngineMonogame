using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenFunction
    : CalculatorToken
{
    private string _functionName;
    private string[] _args;





    public CalculatorTokenFunction(Calculator calculator, string functionName, string[] args)
        : base(calculator)
    {
        _functionName = functionName;
        _args = args;
    }

    public CalculatorTokenFunction(Calculator calculator, XmlElement el, SaveOption option)
        : base(calculator)
    {
        Load(el, option);
    }

    public CalculatorTokenFunction(Calculator calculator, BinaryReader br, SaveOption option)
        : base(calculator)
    {
        Load(br, option);
    }



    public override float Evaluate()
    {
        return Calculator.Parser.EvaluateFunction(_functionName, _args);
    }


    public override void Save(XmlElement el, SaveOption option)
    {
        var node = el.OwnerDocument.CreateElement("Node");
        el.AppendChild(node);
        el.OwnerDocument.AddAttribute(node, "type", ((int)CalculatorTokenType.Function).ToString());
        var valueNode = el.OwnerDocument.CreateElementWithText("FunctionName", _functionName);
        node.AppendChild(valueNode);

        var argNode = el.OwnerDocument.CreateElement("ArgumentList");
        node.AppendChild(argNode);

        foreach (var a in _args)
        {
            valueNode = el.OwnerDocument.CreateElementWithText("Argument", a);
            argNode.AppendChild(valueNode);
        }
    }

    public override void Load(XmlElement el, SaveOption option)
    {
        _functionName = el.SelectSingleNode("FunctionName").InnerText;
        var args = new List<string>();

        foreach (XmlNode n in el.SelectNodes("ArgumentList/Argument"))
        {
            args.Add(n.InnerText);
        }

        _args = args.ToArray();
    }

    public override void Save(BinaryWriter bw, SaveOption option)
    {
        bw.Write((int)CalculatorTokenType.Function);
        bw.Write(_functionName);
        bw.Write(_args.Length);

        foreach (var a in _args)
        {
            bw.Write(a);
        }
    }

    public override void Load(BinaryReader br, SaveOption option)
    {
        br.ReadInt32();
        _functionName = br.ReadString();
        var count = br.ReadInt32();
        _args = new string[count];

        for (var i = 0; i < count; i++)
        {
            _args[i] = br.ReadString();
        }
    }


}
using System.Text.Json;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Parser;

internal class CalculatorTokenSequence : CalculatorToken
{
    public enum TokenSequence
    {
        Sequence,
        StartSequence,
        EndSequence
    }

    private readonly TokenSequence _sequence;

    public TokenSequence Sequence => _sequence;

    public CalculatorTokenSequence(Calculator calculator, TokenSequence sequence)
        : base(calculator)
    {
        _sequence = sequence;
    }

    public override float Evaluate()
    {
        throw new InvalidOperationException("Don't use to evaluate");
    }

    public override void Load(JsonElement element)
    {
        throw new InvalidOperationException("Can't save this object. It is a temporary objecte");
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        throw new InvalidOperationException("Can't save this object. It is a temporary object");
    }

#endif
}
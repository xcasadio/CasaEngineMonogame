using System.Globalization;

namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraphNodeDisplacementTexture : MaterialGraphNode
{
    public float speed = 1f;

    protected override IEnumerable<string> AddConstants()
    {
        yield return "float TotalElapsedTime;";
    }

    public override string GetPixelComputation()
    {
        string uv;

        if (_inputSlot != null) // need UVSlot
        {
            uv = _inputSlot.GetValue();
        }
        else
        {
            uv = "pin.TextureCoordinate0";
        }

        return $"{uv} + ({speed.ToString(CultureInfo.InvariantCulture)} * TotalElapsedTime)";
    }
}
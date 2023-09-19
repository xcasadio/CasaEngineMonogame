namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraphNodeRepetitionTexture : MaterialGraphNode
{
    private float repetition = 2f;

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

        return $"{uv} * {repetition}";
    }
}
using CasaEngine.Framework.Assets.Textures;

namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraphNodeTexture : MaterialGraphNode
{
    public Texture Texture { get; set; }

    protected override IEnumerable<string> AddConstants()
    {
        yield return "DECLARE_TEXTURE(Texture, 0);";
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

        return $"SAMPLE_TEXTURE(Texture, ({uv}))";
    }

}
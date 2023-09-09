using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials;

public abstract class MaterialAsset : IMaterialAssetVisitable
{
    public abstract void Accept(IMaterialAssetVisitor visitor);

    public virtual IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> GetVertexPropertiesNeeded()
    {
        return Enumerable.Empty<Tuple<VertexElementFormat, VertexElementUsage>>();
    }
}


public class MaterialGraph
{
    public MaterialGraphNodeSlot _slotAttached;

    public string Compile()
    {
        return _slotAttached.GetValue();
    }
}

public class MaterialGraphNodeSlot
{
    public int index;
    public MaterialGraphNode Node;

    public string GetValue()
    {
        return Node.GetValue();
    }
}

public abstract class MaterialGraphNode
{
    public MaterialGraphNodeSlot _inputSlot;

    public abstract string GetValue();
}

public class MaterialGraphNodeTexture : MaterialGraphNode
{
    public override string GetValue()
    {
        string uv = "pin.TextureCoordinate0";

        if (_inputSlot != null) // need UVSlot
        {
            uv += _inputSlot.GetValue();
        }

        return $"SAMPLE_TEXTURE(Texture, ({uv}))";
    }
}

public class MaterialGraphNodeDisplacementTexture : MaterialGraphNode
{
    private float inputValue = 2f;

    public override string GetValue()
    {
        return $" * {inputValue}";
    }
}
/*
public class MaterialGraphNodeTextureUV : MaterialGraphNode
{
    public override string GetValue()
    {
        return "pin.TextureCoordinate0";
    }
}*/
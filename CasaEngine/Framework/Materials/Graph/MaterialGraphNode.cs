using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Graph;

public abstract class MaterialGraphNode
{
    public MaterialGraphNodeSlot _inputSlot;

    public IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> GetVertexPropertiesNeeded()
    {
        foreach (var constant in AddVertexPropertiesNeeded())
        {
            yield return constant;
        }

        if (_inputSlot != null) // need UVSlot
        {
            foreach (var vertexProperty in _inputSlot.Node.GetVertexPropertiesNeeded())
            {
                yield return vertexProperty;
            }
        }
    }

    public virtual IEnumerable<Tuple<VertexElementFormat, VertexElementUsage>> AddVertexPropertiesNeeded()
    {
        yield break;
    }

    public IEnumerable<string> GetConstants()
    {
        foreach (var constant in AddConstants())
        {
            yield return constant;
        }

        if (_inputSlot != null) // need UVSlot
        {
            foreach (var constant in _inputSlot.Node.GetConstants())
            {
                yield return constant;
            }
        }
    }

    protected virtual IEnumerable<string> AddConstants()
    {
        yield break;
    }

    public string GetVertexComputation()
    {
        string result = AddVertexComputation();

        if (_inputSlot != null) // need UVSlot
        {
            result += _inputSlot.Node.GetVertexComputation();
        }

        return result;
    }

    public virtual string AddVertexComputation()
    {
        return string.Empty;
    }

    public abstract string GetPixelComputation();

}
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraphNodeSlot
{
    public int index;
    public MaterialGraphNode Node;

    public IEnumerable<string> GetConstants()
    {
        return Node.GetConstants();
    }

    public string GetValue()
    {
        return Node.GetPixelComputation();
    }
}
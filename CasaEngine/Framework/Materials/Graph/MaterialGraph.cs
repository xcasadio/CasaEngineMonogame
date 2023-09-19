namespace CasaEngine.Framework.Materials.Graph;

public class MaterialGraph
{
    public MaterialGraphNodeSlot DiffuseSlot;

    public string[] GetConstants()
    {
        return DiffuseSlot.GetConstants().ToArray();
    }

    public string Compile()
    {
        return DiffuseSlot.GetValue();
    }

}
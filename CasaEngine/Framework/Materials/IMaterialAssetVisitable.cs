namespace CasaEngine.Framework.Materials;

public interface IMaterialAssetVisitable
{
    public void Accept(IMaterialAssetVisitor visitor);
}
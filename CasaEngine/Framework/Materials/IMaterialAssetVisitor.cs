namespace CasaEngine.Framework.Materials;

public interface IMaterialAssetVisitor
{
    public void Visit(MaterialColor materialColor);
    public void Visit(MaterialTexture materialTexture);
}
namespace Veldrid.SceneGraph;

public abstract class Object : IObject
{
    public enum DataVarianceType
    {
        Dynamic,
        Static,
        Unspecified
    };

    public DataVarianceType DataVariance { get; set; } = DataVarianceType.Unspecified;
}
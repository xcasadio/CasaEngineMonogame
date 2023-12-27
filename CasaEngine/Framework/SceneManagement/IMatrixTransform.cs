using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface IMatrixTransform : IGroup
{
    Matrix Matrix { get; }
    Matrix Inverse { get; }
    void PreMultiply(Matrix mat);
    void PostMultiply(Matrix mat);
}
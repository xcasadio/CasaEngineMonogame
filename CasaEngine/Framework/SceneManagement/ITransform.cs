using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface ITransform : IGroup
{
    Transform.ReferenceFrameType ReferenceFrame { get; set; }
    bool ComputeLocalToWorldMatrix(ref Matrix matrix, NodeVisitor visitor);
    bool ComputeWorldToLocalMatrix(ref Matrix matrix, NodeVisitor visitor);
}
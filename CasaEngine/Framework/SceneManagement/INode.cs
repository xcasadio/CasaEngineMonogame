using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public interface INode : IObject
{
    Guid Id { get; }
    uint NodeMask { get; set; }
    string NameString { get; set; }
    int NumParents { get; }
    bool CullingActive { get; set; }
    int NumChildrenWithCullingDisabled { get; set; }
    bool IsCullingActive { get; }
    BoundingSphere InitialBound { get; set; }
    int GetNumChildrenRequiringEventTraversal();
    int GetNumChildrenRequiringUpdateTraversal();
    void SetNumChildrenRequiringEventTraversal(int i);
    void SetNumChildrenRequiringUpdateTraversal(int i);
    event Func<Node, BoundingSphere> ComputeBoundCallback;

    void SetUpdateCallback(Action<INodeVisitor, INode> callback);
    Action<INodeVisitor, INode> GetUpdateCallback();


    void AddParent(IGroup parent);
    void RemoveParent(IGroup parent);

    /// <summary>
    /// Mark this node's bounding sphere dirty.  Forcing it to be computed on the next call
    /// to GetBound();
    /// </summary>
    void DirtyBound();

    /// <summary>
    /// Get the bounding sphere for this node.
    /// </summary>
    /// <returns></returns>
    BoundingSphere GetBound();

    /// <summary>
    /// Compute the bounding sphere of this geometry
    /// </summary>
    /// <returns></returns>
    BoundingSphere ComputeBound();

    void Accept(INodeVisitor nv);
    void Ascend(INodeVisitor nv);
    void Traverse(INodeVisitor nv);
}
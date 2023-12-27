namespace CasaEngine.Framework.SceneManagement;

public interface IGroup : INode
{
    bool AddChild(INode child);
    bool InsertChild(int index, INode child);
    bool RemoveChild(INode child);
    bool RemoveChildren(int pos, int numChildrenToRemove);
    void ChildInserted(int index);
    void ChildRemoved(int index, int count);
    int GetNumChildren();
}
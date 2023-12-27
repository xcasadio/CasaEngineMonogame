using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public class Group : Node, IGroup
{
    protected List<Tuple<INode, bool>> _children = new();

    public static IGroup Create()
    {
        return new Group();
    }

    public bool AddChild(INode child)
    {
        return InsertChild(_children.Count, child);
    }

    public virtual bool AddChild(INode child, bool value)
    {
        return InsertChild(_children.Count, child, value);
    }

    public bool InsertChild(int index, INode child)
    {
        return InsertChild(index, child, true);
    }

    public bool InsertChild(int index, INode child, bool value)
    {
        if (null == child)
        {
            return false;
        }

        if (_children.Exists(x => x.Item1.Id == child.Id))
        {
            //_logger.Error(m => m($"Child [{child.Id}] already exists in group!"));
            return false;
        }

        if (index >= _children.Count)
        {
            index = _children.Count;
            _children.Add(Tuple.Create(child, value));
        }
        else
        {
            _children.Insert(index, Tuple.Create(child, value));
        }

        child.AddParent(this);

        ChildInserted(index);

        DirtyBound();

        return true;
    }

    public virtual bool RemoveChild(INode child)
    {
        var pos = _children.FindIndex(x => x.Item1.Id == child.Id);
        return pos < _children.Count && RemoveChildren(pos, 1);
    }

    public virtual bool RemoveChildren(int pos, int numChildrenToRemove)
    {
        if (pos > _children.Count || numChildrenToRemove <= 0)
        {
            return false;
        }

        var endOfRemoveRange = pos + numChildrenToRemove;
        if (endOfRemoveRange > _children.Count)
        {
            // TODO add logging
            endOfRemoveRange = _children.Count;
        }

        for (var i = pos; i < endOfRemoveRange; ++i)
        {
            var child = _children[i];
            child.Item1.RemoveParent(this);
        }

        _children.RemoveRange(pos, numChildrenToRemove);

        ChildRemoved(pos, endOfRemoveRange - pos);

        DirtyBound();

        return true;
    }


    public virtual void ChildInserted(int index)
    {
        // Do nothing by default
    }

    public virtual void ChildRemoved(int index, int count)
    {
        // Do nothing by default
    }

    public int GetNumChildren()
    {
        return _children.Count;
    }

    public override void Traverse(INodeVisitor nv)
    {
        foreach (var child in _children)
        {
            child.Item1.Accept(nv);
        }
    }

    public override BoundingSphere ComputeBound()
    {
        var bsphere = BoundingSphereExtension.Create();
        if (0 == _children.Count)
        {
            return bsphere;
        }

        // note, special handling of the case when a child is an Transform,
        // such that only Transforms which are relative to their parents coordinates frame (i.e this group)
        // are handled, Transform relative to and absolute reference frame are ignored.

        var bb = BoundingBoxHelper.Create();
        foreach (var child in _children)
        {
            switch (child.Item1)
            {
                case Transform transform when transform.ReferenceFrame != Transform.ReferenceFrameType.Relative:
                    continue;
                /*case Geode geode:
                    bb.ExpandBy(geode.GetBoundingBox());
                    break;*/
                default:
                    var bs = child.Item1.GetBound();
                    bb.ExpandBy(bs);
                    break;
            }
        }

        if (!bb.Valid())
        {
            return bsphere;
        }

        bsphere.Center = bb.GetCenter();
        bsphere.Radius = 0.0f;
        foreach (var child in _children)
        {
            if (child.Item1 is Transform transform &&
                transform.ReferenceFrame != Transform.ReferenceFrameType.Relative)
            {
                continue;
            }

            var bs = child.Item1.GetBound();
            bsphere.ExpandRadiusBy(bs);
        }

        return bsphere;
    }
}
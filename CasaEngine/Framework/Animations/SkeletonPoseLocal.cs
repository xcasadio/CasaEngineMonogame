using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class SkeletonPoseLocal
{
    private readonly BoneTransform[] _localTransforms;

    public SkeletonPoseLocal(SkeletonDefinition skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        _localTransforms = new BoneTransform[skeleton.Count];
        ResetToBindPose();
    }

    public SkeletonDefinition Skeleton { get; }

    public int Count => _localTransforms.Length;

    public bool IsDirty { get; private set; }

    public int DirtyStartIndex { get; private set; }

    public uint Version { get; private set; }

    public BoneTransform GetTransform(int index)
    {
        ValidateJointIndex(index);
        return _localTransforms[index];
    }

    public Matrix GetTransformMatrix(int index)
    {
        return GetTransform(index).ToMatrix();
    }

    public void SetTransform(int index, BoneTransform transform)
    {
        ValidateJointIndex(index);

        if (_localTransforms[index] == transform)
        {
            return;
        }

        _localTransforms[index] = transform;
        MarkDirty(index);
    }

    public void SetTransformMatrix(int index, Matrix matrix)
    {
        SetTransform(index, BoneTransform.FromMatrix(matrix));
    }

    public void ResetToBindPose()
    {
        for (var index = 0; index < _localTransforms.Length; index++)
        {
            SetTransformDirect(index, Skeleton.GetBindLocalTransform(index));
        }

        MarkDirtyFrom(0);
    }

    public void CopyFrom(SkeletonPoseLocal other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!ReferenceEquals(other.Skeleton, Skeleton))
        {
            throw new ArgumentException("Cannot copy a pose from a different skeleton definition.", nameof(other));
        }

        Array.Copy(other._localTransforms, _localTransforms, _localTransforms.Length);
        MarkDirtyFrom(0);
    }

    public void ClearDirty()
    {
        IsDirty = false;
        DirtyStartIndex = Count;
    }

    private void MarkDirty(int index)
    {
        MarkDirtyFrom(index);
    }

    internal void SetTransformDirect(int index, BoneTransform transform)
    {
        ValidateJointIndex(index);
        _localTransforms[index] = transform;
    }

    internal void MarkDirtyFrom(int startIndex)
    {
        if (!IsDirty || startIndex < DirtyStartIndex)
        {
            DirtyStartIndex = startIndex;
        }

        IsDirty = true;
        Version++;
    }

    private void ValidateJointIndex(int index)
    {
        if ((uint)index >= (uint)_localTransforms.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
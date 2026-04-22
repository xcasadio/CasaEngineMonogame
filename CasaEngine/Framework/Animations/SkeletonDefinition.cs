using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class SkeletonDefinition
{
    private readonly SkeletonJointDefinition[] _joints;
    private readonly ReadOnlyCollection<SkeletonJointDefinition> _jointView;
    private readonly Dictionary<string, int> _jointIndicesByName;

    public SkeletonDefinition(IReadOnlyList<SkeletonJointDefinition> joints)
    {
        ArgumentNullException.ThrowIfNull(joints);

        if (joints.Count == 0)
        {
            throw new ArgumentException("A skeleton needs at least one joint.", nameof(joints));
        }

        _joints = new SkeletonJointDefinition[joints.Count];
        _jointView = Array.AsReadOnly(_joints);
        _jointIndicesByName = new Dictionary<string, int>(joints.Count, StringComparer.Ordinal);

        var rootIndex = -1;

        for (var index = 0; index < joints.Count; index++)
        {
            var joint = joints[index];

            if (string.IsNullOrWhiteSpace(joint.Name))
            {
                throw new ArgumentException($"Joint at index {index} has no name.", nameof(joints));
            }

            if (joint.ParentIndex < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(joints), $"Joint '{joint.Name}' has an invalid parent index {joint.ParentIndex}.");
            }

            if (joint.ParentIndex >= index)
            {
                throw new ArgumentException($"Joint '{joint.Name}' must reference a parent that appears before it in the skeleton definition.", nameof(joints));
            }

            if (!_jointIndicesByName.TryAdd(joint.Name, index))
            {
                throw new ArgumentException($"Joint name '{joint.Name}' is duplicated in the skeleton definition.", nameof(joints));
            }

            if (joint.ParentIndex == -1)
            {
                if (rootIndex != -1)
                {
                    throw new ArgumentException("A skeleton definition can only contain a single root joint.", nameof(joints));
                }

                rootIndex = index;
            }

            _joints[index] = joint;
        }

        if (rootIndex == -1)
        {
            throw new ArgumentException("A skeleton definition requires a root joint.", nameof(joints));
        }

        RootIndex = rootIndex;
    }

    public int Count => _joints.Length;

    public int RootIndex { get; }

    public IReadOnlyList<SkeletonJointDefinition> Joints => _jointView;

    public SkeletonJointDefinition GetJoint(int index)
    {
        ValidateJointIndex(index);
        return _joints[index];
    }

    public BoneTransform GetBindLocalTransform(int index)
    {
        ValidateJointIndex(index);
        return _joints[index].LocalBindTransform;
    }

    public Matrix GetInverseBindMatrix(int index)
    {
        ValidateJointIndex(index);
        return _joints[index].InverseBindMatrix;
    }

    public bool TryGetJointIndex(string jointName, out int index)
    {
        ArgumentException.ThrowIfNullOrEmpty(jointName);
        return _jointIndicesByName.TryGetValue(jointName, out index);
    }

    public SkeletonPoseLocal CreateLocalBindPose()
    {
        return new SkeletonPoseLocal(this);
    }

    private void ValidateJointIndex(int index)
    {
        if ((uint)index >= (uint)_joints.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
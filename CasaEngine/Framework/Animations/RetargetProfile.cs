using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public enum RetargetReferencePoseMode
{
    BindPose,
}

public enum RetargetAxis
{
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY,
    PositiveZ,
    NegativeZ,
}

public sealed class RetargetProfile
{
    private readonly RetargetJointMapping[] _jointMappings;
    private readonly int[] _mappingIndicesBySourceJointIndex;

    public RetargetProfile(
        SkeletonDefinition sourceSkeleton,
        SkeletonDefinition targetSkeleton,
        IReadOnlyList<RetargetJointMapping> jointMappings,
        RetargetReferencePoseMode referencePoseMode = RetargetReferencePoseMode.BindPose,
        RetargetAxis sourceForwardAxis = RetargetAxis.PositiveZ,
        RetargetAxis sourceUpAxis = RetargetAxis.PositiveY,
        RetargetAxis targetForwardAxis = RetargetAxis.PositiveZ,
        RetargetAxis targetUpAxis = RetargetAxis.PositiveY,
        float rootTranslationScale = 1f)
    {
        SourceSkeleton = sourceSkeleton ?? throw new ArgumentNullException(nameof(sourceSkeleton));
        TargetSkeleton = targetSkeleton ?? throw new ArgumentNullException(nameof(targetSkeleton));
        ArgumentNullException.ThrowIfNull(jointMappings);

        if (rootTranslationScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(rootTranslationScale));
        }

        ValidateAxes(sourceForwardAxis, sourceUpAxis, nameof(sourceForwardAxis), nameof(sourceUpAxis));
        ValidateAxes(targetForwardAxis, targetUpAxis, nameof(targetForwardAxis), nameof(targetUpAxis));

        ReferencePoseMode = referencePoseMode;
        SourceForwardAxis = sourceForwardAxis;
        SourceUpAxis = sourceUpAxis;
        TargetForwardAxis = targetForwardAxis;
        TargetUpAxis = targetUpAxis;
        RootTranslationScale = rootTranslationScale;

        _jointMappings = new RetargetJointMapping[jointMappings.Count];
        _mappingIndicesBySourceJointIndex = new int[sourceSkeleton.Count];
        Array.Fill(_mappingIndicesBySourceJointIndex, -1);

        for (var mappingIndex = 0; mappingIndex < jointMappings.Count; mappingIndex++)
        {
            var jointMapping = jointMappings[mappingIndex] ?? throw new ArgumentException("Joint mappings cannot contain null entries.", nameof(jointMappings));
            if (!ReferenceEquals(jointMapping.SourceSkeleton, sourceSkeleton))
            {
                throw new ArgumentException("All retarget mappings must reference the source skeleton passed to the profile.", nameof(jointMappings));
            }

            if (!ReferenceEquals(jointMapping.TargetSkeleton, targetSkeleton))
            {
                throw new ArgumentException("All retarget mappings must reference the target skeleton passed to the profile.", nameof(jointMappings));
            }

            if (_mappingIndicesBySourceJointIndex[jointMapping.SourceJointIndex] != -1)
            {
                throw new ArgumentException($"Source joint '{jointMapping.SourceJointName}' is mapped more than once.", nameof(jointMappings));
            }

            _jointMappings[mappingIndex] = jointMapping;
            _mappingIndicesBySourceJointIndex[jointMapping.SourceJointIndex] = mappingIndex;
        }
    }

    public SkeletonDefinition SourceSkeleton { get; }

    public SkeletonDefinition TargetSkeleton { get; }

    public RetargetReferencePoseMode ReferencePoseMode { get; }

    public RetargetAxis SourceForwardAxis { get; }

    public RetargetAxis SourceUpAxis { get; }

    public RetargetAxis TargetForwardAxis { get; }

    public RetargetAxis TargetUpAxis { get; }

    public float RootTranslationScale { get; }

    public IReadOnlyList<RetargetJointMapping> JointMappings => _jointMappings;

    public Vector3 SourceForwardVector => GetAxisVector(SourceForwardAxis);

    public Vector3 SourceUpVector => GetAxisVector(SourceUpAxis);

    public Vector3 TargetForwardVector => GetAxisVector(TargetForwardAxis);

    public Vector3 TargetUpVector => GetAxisVector(TargetUpAxis);

    public bool TryGetJointMapping(int sourceJointIndex, out RetargetJointMapping jointMapping)
    {
        if ((uint)sourceJointIndex >= (uint)_mappingIndicesBySourceJointIndex.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceJointIndex));
        }

        var mappingIndex = _mappingIndicesBySourceJointIndex[sourceJointIndex];
        if (mappingIndex == -1)
        {
            jointMapping = null!;
            return false;
        }

        jointMapping = _jointMappings[mappingIndex];
        return true;
    }

    public bool TryGetTargetJointIndex(int sourceJointIndex, out int targetJointIndex)
    {
        if (TryGetJointMapping(sourceJointIndex, out var jointMapping))
        {
            targetJointIndex = jointMapping.TargetJointIndex;
            return true;
        }

        targetJointIndex = -1;
        return false;
    }

    public static Vector3 GetAxisVector(RetargetAxis axis)
    {
        return axis switch
        {
            RetargetAxis.PositiveX => Vector3.UnitX,
            RetargetAxis.NegativeX => -Vector3.UnitX,
            RetargetAxis.PositiveY => Vector3.UnitY,
            RetargetAxis.NegativeY => -Vector3.UnitY,
            RetargetAxis.PositiveZ => Vector3.UnitZ,
            RetargetAxis.NegativeZ => -Vector3.UnitZ,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null),
        };
    }

    private static void ValidateAxes(RetargetAxis forwardAxis, RetargetAxis upAxis, string forwardParameterName, string upParameterName)
    {
        var forward = GetAxisVector(forwardAxis);
        var up = GetAxisVector(upAxis);
        if (Vector3.Cross(forward, up).LengthSquared() <= float.Epsilon)
        {
            throw new ArgumentException("Forward and up axes must not be colinear.", $"{forwardParameterName}, {upParameterName}");
        }
    }
}

public sealed class RetargetJointMapping
{
    public RetargetJointMapping(
        SkeletonDefinition sourceSkeleton,
        SkeletonDefinition targetSkeleton,
        string sourceJointName,
        int sourceJointIndex,
        string targetJointName,
        int targetJointIndex,
        float translationScale = 1f)
    {
        SourceSkeleton = sourceSkeleton ?? throw new ArgumentNullException(nameof(sourceSkeleton));
        TargetSkeleton = targetSkeleton ?? throw new ArgumentNullException(nameof(targetSkeleton));

        if (string.IsNullOrWhiteSpace(sourceJointName))
        {
            throw new ArgumentException("Source joint name is required.", nameof(sourceJointName));
        }

        if (string.IsNullOrWhiteSpace(targetJointName))
        {
            throw new ArgumentException("Target joint name is required.", nameof(targetJointName));
        }

        if ((uint)sourceJointIndex >= (uint)sourceSkeleton.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceJointIndex));
        }

        if ((uint)targetJointIndex >= (uint)targetSkeleton.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(targetJointIndex));
        }

        if (translationScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(translationScale));
        }

        SourceJointName = sourceJointName;
        SourceJointIndex = sourceJointIndex;
        TargetJointName = targetJointName;
        TargetJointIndex = targetJointIndex;
        TranslationScale = translationScale;
    }

    public SkeletonDefinition SourceSkeleton { get; }

    public SkeletonDefinition TargetSkeleton { get; }

    public string SourceJointName { get; }

    public int SourceJointIndex { get; }

    public string TargetJointName { get; }

    public int TargetJointIndex { get; }

    public float TranslationScale { get; }
}
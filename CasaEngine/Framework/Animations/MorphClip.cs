namespace CasaEngine.Framework.Animations;

public sealed class MorphClip
{
    private readonly MorphChannel[] _channels;

    public MorphClip(string name, IReadOnlyList<MorphChannel> channels, float durationSeconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Morph clips need a name.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(channels);

        Name = name;
        _channels = new MorphChannel[channels.Count];

        var computedDuration = 0f;
        for (var index = 0; index < channels.Count; index++)
        {
            var channel = channels[index] ?? throw new ArgumentException("Morph channels cannot contain null entries.", nameof(channels));
            _channels[index] = channel;
            computedDuration = Math.Max(computedDuration, channel.EndTimeSeconds);
        }

        if (durationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (durationSeconds > 0f && durationSeconds < computedDuration)
        {
            throw new ArgumentException("Morph clip duration cannot be shorter than the last keyframe time.", nameof(durationSeconds));
        }

        DurationSeconds = durationSeconds > 0f ? durationSeconds : computedDuration;
    }

    public string Name { get; }

    public float DurationSeconds { get; }

    public IReadOnlyList<MorphChannel> Channels => _channels;
}

public sealed class MorphChannel
{
    private readonly MorphKeyframe[] _keyframes;

    public MorphChannel(string meshName, int meshIndex, IReadOnlyList<MorphKeyframe> keyframes)
    {
        if (string.IsNullOrWhiteSpace(meshName))
        {
            throw new ArgumentException("Morph channels need a mesh name.", nameof(meshName));
        }

        if (meshIndex < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(meshIndex));
        }

        ArgumentNullException.ThrowIfNull(keyframes);

        MeshName = meshName;
        MeshIndex = meshIndex;
        _keyframes = new MorphKeyframe[keyframes.Count];

        var endTimeSeconds = 0f;
        for (var index = 0; index < keyframes.Count; index++)
        {
            var keyframe = keyframes[index] ?? throw new ArgumentException("Morph keyframes cannot contain null entries.", nameof(keyframes));
            _keyframes[index] = keyframe;
            endTimeSeconds = Math.Max(endTimeSeconds, keyframe.TimeSeconds);
        }

        EndTimeSeconds = endTimeSeconds;
    }

    public string MeshName { get; }

    public int MeshIndex { get; }

    public float EndTimeSeconds { get; }

    public IReadOnlyList<MorphKeyframe> Keyframes => _keyframes;
}

public sealed class MorphKeyframe
{
    private readonly int[] _attachmentIndices;
    private readonly float[] _weights;

    public MorphKeyframe(float timeSeconds, IReadOnlyList<int> attachmentIndices, IReadOnlyList<float> weights)
    {
        if (timeSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        }

        ArgumentNullException.ThrowIfNull(attachmentIndices);
        ArgumentNullException.ThrowIfNull(weights);

        if (attachmentIndices.Count != weights.Count)
        {
            throw new ArgumentException("Morph keyframe target indices and weights must have the same length.");
        }

        TimeSeconds = timeSeconds;
        _attachmentIndices = new int[attachmentIndices.Count];
        _weights = new float[weights.Count];

        for (var index = 0; index < attachmentIndices.Count; index++)
        {
            _attachmentIndices[index] = attachmentIndices[index];
            _weights[index] = weights[index];
        }
    }

    public float TimeSeconds { get; }

    public IReadOnlyList<int> AttachmentIndices => _attachmentIndices;

    public IReadOnlyList<float> Weights => _weights;
}
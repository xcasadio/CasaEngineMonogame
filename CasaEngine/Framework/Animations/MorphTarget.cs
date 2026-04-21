using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class MorphTarget
{
    private readonly Vector3[] _positions;
    private readonly Vector3[] _normals;
    private readonly Vector3[] _tangents;
    private readonly Vector3[] _biTangents;
    private readonly Vector3[][] _textureCoordinateChannels;
    private readonly Vector4[][] _vertexColorChannels;

    public MorphTarget(
        int meshIndex,
        int attachmentIndex,
        string meshName,
        string name,
        float defaultWeight,
        IReadOnlyList<Vector3>? positions = null,
        IReadOnlyList<Vector3>? normals = null,
        IReadOnlyList<Vector3>? tangents = null,
        IReadOnlyList<Vector3>? biTangents = null,
        IReadOnlyList<Vector3[]>? textureCoordinateChannels = null,
        IReadOnlyList<Vector4[]>? vertexColorChannels = null)
    {
        if (meshIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(meshIndex));
        }

        if (attachmentIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attachmentIndex));
        }

        if (string.IsNullOrWhiteSpace(meshName))
        {
            throw new ArgumentException("Morph targets need a mesh name.", nameof(meshName));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Morph targets need a name.", nameof(name));
        }

        MeshIndex = meshIndex;
        AttachmentIndex = attachmentIndex;
        MeshName = meshName;
        Name = name;
        DefaultWeight = defaultWeight;
        _positions = CopyValues(positions);
        _normals = CopyValues(normals);
        _tangents = CopyValues(tangents);
        _biTangents = CopyValues(biTangents);
        _textureCoordinateChannels = CopyChannels(textureCoordinateChannels);
        _vertexColorChannels = CopyChannels(vertexColorChannels);
    }

    public int MeshIndex { get; }

    public int AttachmentIndex { get; }

    public string MeshName { get; }

    public string Name { get; }

    public float DefaultWeight { get; }

    public IReadOnlyList<Vector3> Positions => _positions;

    public IReadOnlyList<Vector3> Normals => _normals;

    public IReadOnlyList<Vector3> Tangents => _tangents;

    public IReadOnlyList<Vector3> BiTangents => _biTangents;

    public IReadOnlyList<Vector3[]> TextureCoordinateChannels => _textureCoordinateChannels;

    public IReadOnlyList<Vector4[]> VertexColorChannels => _vertexColorChannels;

    private static T[] CopyValues<T>(IReadOnlyList<T>? values)
    {
        if (values == null || values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var copy = new T[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return copy;
    }

    private static T[][] CopyChannels<T>(IReadOnlyList<T[]>? channels)
    {
        if (channels == null || channels.Count == 0)
        {
            return Array.Empty<T[]>();
        }

        var copy = new T[channels.Count][];
        for (var channelIndex = 0; channelIndex < channels.Count; channelIndex++)
        {
            var sourceChannel = channels[channelIndex];
            if (sourceChannel == null || sourceChannel.Length == 0)
            {
                copy[channelIndex] = Array.Empty<T>();
                continue;
            }

            var channelCopy = new T[sourceChannel.Length];
            Array.Copy(sourceChannel, channelCopy, sourceChannel.Length);
            copy[channelIndex] = channelCopy;
        }

        return copy;
    }
}
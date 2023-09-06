using System.Collections.Generic;

namespace EditorWpf.Shaders;

internal class ShaderCompiled
{
    public List<string> Dependencies { get; } = new();

    public byte[]? ByteCode { get; set; }

    public void AddDependency(string file)
    {
        Dependencies.Add(file);
    }
}
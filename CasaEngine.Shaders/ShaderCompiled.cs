using System.Collections.Generic;

namespace CasaEngine.Shaders;

public class ShaderCompiled
{
    public List<string> Dependencies { get; } = new();

    public byte[] ByteCode { get; set; }
    public string Logs { get; set; }

    public void AddDependency(string file)
    {
        Dependencies.Add(file);
    }
}
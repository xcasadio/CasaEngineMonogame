using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Shaders;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public sealed class ShaderDependencyIndexTests : IDisposable
{
    private readonly string _contentRootDirectory;

    public ShaderDependencyIndexTests()
    {
        _contentRootDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_ShaderDeps", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRootDirectory);
    }

    [Fact]
    public void GetAffectedRootShaders_ReturnsAllRootsForNestedSharedInclude()
    {
        WriteRelativeFile("Shaders/LitForward.fx", "#include \"Includes/Common.fxh\"\n");
        WriteRelativeFile("Shaders/SkyCubemap.fx", "#include \"Includes/Common.fxh\"\n");
        WriteRelativeFile("Shaders/ShadowDepth.fx", "#include \"Macros.fxh\"\n");
        WriteRelativeFile("Shaders/Includes/Common.fxh", "#include \"Nested/Lighting.fxh\"\n");
        WriteRelativeFile("Shaders/Includes/Nested/Lighting.fxh", "float4 LightingValue;\n");
        WriteRelativeFile("Shaders/Macros.fxh", "float4 MacroValue;\n");

        var dependencyIndex = new ShaderDependencyIndex(
            _contentRootDirectory,
            new[]
            {
                "Shaders/LitForward.fx",
                "Shaders/SkyCubemap.fx",
                "Shaders/ShadowDepth.fx",
            });

        var affectedRoots = Sort(dependencyIndex.GetAffectedRootShaders("Shaders/Includes/Nested/Lighting.fxh"));

        Assert.Equal(2, affectedRoots.Count);
        Assert.Equal("Shaders/LitForward.fx", affectedRoots[0]);
        Assert.Equal("Shaders/SkyCubemap.fx", affectedRoots[1]);
    }

    [Fact]
    public void GetAffectedRootShaders_KeepsMissingIncludeDependencyForFutureCreates()
    {
        WriteRelativeFile("Shaders/LitForward.fx", "#include \"Includes/Missing.fxh\"\n");

        var dependencyIndex = new ShaderDependencyIndex(
            _contentRootDirectory,
            new[]
            {
                "Shaders/LitForward.fx",
            });

        var affectedRoots = dependencyIndex.GetAffectedRootShaders("Shaders/Includes/Missing.fxh");
        string affectedRoot = Assert.Single(affectedRoots);

        Assert.Equal("Shaders/LitForward.fx", affectedRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRootDirectory))
        {
            Directory.Delete(_contentRootDirectory, true);
        }
    }

    private void WriteRelativeFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(
            _contentRootDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Shader test file path must have a directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
    }

    private static List<string> Sort(IReadOnlyCollection<string> values)
    {
        var sortedValues = new List<string>(values);
        sortedValues.Sort(StringComparer.OrdinalIgnoreCase);
        return sortedValues;
    }
}
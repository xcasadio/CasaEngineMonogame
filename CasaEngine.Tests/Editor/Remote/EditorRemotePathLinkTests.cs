using System.Diagnostics;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// ADR-0093, deferred finding A3: a project path that passes the text containment check is still refused when a
/// component below the project root is a link. Junctions are made with <c>mklink /J</c> (no privilege needed) and
/// checked to exist before each assertion, so no test passes on a missing link.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemotePathLinkTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-links-" + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly string _outside;
    private readonly List<string> _links = new();
    private readonly ITestOutputHelper _log;
    private RemoteTestHost _host;
    private EditorRemoteControl _control;

    public EditorRemotePathLinkTests(ITestOutputHelper log)
    {
        _log = log;
        _project = Path.GetFullPath(Path.Combine(_directory, "proj"));
        _outside = Path.GetFullPath(Path.Combine(_directory, "outside"));
        Directory.CreateDirectory(_project);
        Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_outside, "a.world"), "{}");
    }

    public void Dispose()
    {
        _control?.Dispose();

        // A link is removed on its own first, so that nothing walks through it into its target.
        for (int i = _links.Count - 1; i >= 0; i--)
        {
            if (File.Exists(_links[i]))
            {
                File.Delete(_links[i]);
            }
            else if (Directory.Exists(_links[i]))
            {
                Directory.Delete(_links[i]);
            }
        }

        Directory.Delete(_directory, recursive: true);
    }

    private string CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using (Process process = Process.Start(start)!)
        {
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"mklink /J failed ({process.ExitCode}): {output}");
        }

        _links.Add(link);
        Assert.True(Directory.Exists(link), $"The junction '{link}' was not created.");
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, $"'{link}' is not a reparse point.");
        Assert.NotNull(new DirectoryInfo(link).LinkTarget);
        _log.WriteLine($"junction created: '{link}' -> '{target}'");
        return link;
    }

    private RemoteResponse Call(string method, object parameters)
    {
        _host ??= new RemoteTestHost { ProjectDirectory = _project };
        _control ??= new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = RemoteMessageCodec.ToElement(parameters),
        });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        Assert.True(call.IsCompleted);
        return call.Completion.Result;
    }

    [Fact]
    public void ResolveInsideProject_RefusesAFileUnderAJunction()
    {
        string junction = CreateJunction(Path.Combine(_project, "linked"), _outside);
        Assert.True(File.Exists(Path.Combine(junction, "a.world")));

        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "linked/a.world", out string fullPath, out string linkPath));
        Assert.Null(fullPath);
        Assert.Equal(junction, linkPath);
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "linked/a.world", out _));
    }

    [Fact]
    public void ResolveInsideProject_RefusesANewFileUnderAJunction()
    {
        string junction = CreateJunction(Path.Combine(_project, "linked"), _outside);
        Assert.False(File.Exists(Path.Combine(junction, "new.material")));

        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "linked/new.material", out _, out string linkPath));
        Assert.Equal(junction, linkPath);
    }

    [Fact]
    public void ResolveInsideProject_RefusesANestedJunction()
    {
        Directory.CreateDirectory(Path.Combine(_project, "sub"));
        string junction = CreateJunction(Path.Combine(_project, "sub", "linked"), _outside);

        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "sub/linked/x", out _, out string linkPath));
        Assert.Equal(junction, linkPath);
    }

    [Fact]
    public void ResolveInsideProject_AcceptsAProjectRootThatIsAJunction()
    {
        string root = CreateJunction(Path.Combine(_directory, "proj-link"), _project);
        File.WriteAllText(Path.Combine(_project, "a.world"), "{}");

        Assert.True(EditorRemoteValidation.TryResolveInsideProject(root, "a.world", out string fullPath, out string linkPath));
        Assert.Equal(Path.Combine(root, "a.world"), fullPath);
        Assert.Null(linkPath);
    }

    [Fact]
    public void ResolveInsideProject_AcceptsAMissingNestedPath()
    {
        Assert.True(EditorRemoteValidation.TryResolveInsideProject(_project, "missing/deeper/a.world", out string fullPath, out string linkPath));
        Assert.Equal(Path.Combine(_project, "missing", "deeper", "a.world"), fullPath);
        Assert.Null(linkPath);
    }

    [Fact]
    public void ResolveInsideProject_TextualRefusal_HasNoLinkPath()
    {
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "../outside/a.world", out _, out string linkPath));
        Assert.Null(linkPath);
    }

    /// <summary>A file symbolic link needs a privilege (or developer mode); without it this case is reported as not run.</summary>
    [Fact]
    public void ResolveInsideProject_RefusesAFileSymbolicLink()
    {
        string link = Path.Combine(_project, "link.world");
        try
        {
            File.CreateSymbolicLink(link, Path.Combine(_outside, "a.world"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.WriteLine($"inconclusive: symlink creation not permitted ({exception.GetType().Name}: {exception.Message})");
            return;
        }

        _links.Add(link);
        Assert.NotNull(new FileInfo(link).LinkTarget);
        _log.WriteLine($"file symlink created: '{link}'");

        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "link.world", out _, out string linkPath));
        Assert.Equal(link, linkPath);
    }

    [Fact]
    public void AutomationPath_RefusesAJunctionAndAlternateDataStreams()
    {
        string junction = CreateJunction(Path.Combine(_project, "linked"), _outside);

        Assert.False(EditorRemoteValidation.TryResolveAutomationPath(_project, Path.Combine(junction, "a.world"), out _, out string linkError));
        Assert.Contains(junction, linkError);
        Assert.False(EditorRemoteValidation.TryResolveAutomationPath(_project, Path.Combine(_project, "x.particle:s"), out _, out string streamError));
        Assert.Contains("':'", streamError);
        Assert.False(EditorRemoteValidation.TryResolveAutomationPath(_project, Path.Combine(_outside, "a.world"), out _, out _));
    }

    [Fact]
    public void AutomationPath_AcceptsANormalPathAndTheProjectRoot()
    {
        Assert.True(EditorRemoteValidation.TryResolveAutomationPath(_project, Path.Combine(_project, "Particles", "a.particle"), out string relativePath, out string error));
        Assert.Equal(Path.Combine("Particles", "a.particle"), relativePath);
        Assert.Null(error);

        Assert.True(EditorRemoteValidation.TryResolveAutomationPath(_project, _project, out string rootRelativePath, out _));
        Assert.Equal(string.Empty, rootRelativePath);
    }

    [Fact]
    public void AssetResolve_UnderAJunction_NamesTheJunction()
    {
        string junction = CreateJunction(Path.Combine(_project, "linked"), _outside);

        var refusal = Assert.Throws<EditorRemoteArgumentException>(() => EditorRemoteAssets.Resolve(_project, "linked/a.world", null));
        Assert.Contains(junction, refusal.Message);

        RemoteResponse response = Call("asset.open", new { asset = "linked/a.world" });
        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains(junction, response.Error.Message);
    }

    [Fact]
    public void MaterialCreate_InAJunctionFolder_NamesTheJunction()
    {
        string junction = CreateJunction(Path.Combine(_project, "linked"), _outside);

        RemoteResponse response = Call("material.create", new { name = "Fresh", folder = "linked" });
        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Contains(junction, response.Error.Message);
        Assert.False(File.Exists(Path.Combine(_outside, "Fresh.material")));
    }
}

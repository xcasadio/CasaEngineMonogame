using CasaEngine.Editor.Remote;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// Plan remote-path-segments P2, P3 and P5: Windows reads a segment ending with a dot or a space as another entry than the
/// one the access goes through, so the shared link check refuses every segment of the normalized project-relative path
/// whose last character is '.' or a white space, existing or not, and the refusal says so instead of speaking of a link.
/// Each test is tagged with its set: A ("fails before the rule, passes after") or B ("passes before and after").
/// The fixture has real folders 'Real', 'sub\Real', 'v1.2' and '.hidden'; 'Absent' does not exist.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemotePathSegmentTests : IDisposable
{
    private const string LinkWording = "a link (or an entry that cannot be read)";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-segments-" + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly string _outside;
    private readonly List<string> _links = new();
    private readonly ITestOutputHelper _log;

    public EditorRemotePathSegmentTests(ITestOutputHelper log)
    {
        _log = log;
        _project = Path.GetFullPath(Path.Combine(_directory, "proj"));
        _outside = Path.GetFullPath(Path.Combine(_directory, "outside"));
        Directory.CreateDirectory(Path.Combine(_project, "Real"));
        Directory.CreateDirectory(Path.Combine(_project, "sub", "Real"));
        Directory.CreateDirectory(Path.Combine(_project, "v1.2"));
        Directory.CreateDirectory(Path.Combine(_project, ".hidden"));
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        PathSegmentJunctions.RemoveLinks(_links);
        PathSegmentJunctions.DeleteTreeWithoutLinks(_directory);
    }

    /// <summary>The wording plan P3 gives for a component refused for its last character.</summary>
    private static string NameWording(string component)
    {
        char last = component[^1];
        return last switch
        {
            '.' => "ends with a dot",
            ' ' => "ends with a space",
            _ => "ends with a white-space character",
        };
    }

    // --- set A1: TryResolveInsideProject, text only (no link) -------------------------------------------------------

    /// <summary>
    /// Set A1. Each path is accepted by the link check before the rule: the attributes read are those of the real folder
    /// (trailing space or dot dropped from the last segment) or of a missing entry. <paramref name="component"/> is the
    /// refused component, relative to the project, in its normalized spelling.
    /// </summary>
    [Theory]
    [InlineData("Real /a.world", "Real ")]
    [InlineData("Real../a.world", "Real..")]
    [InlineData("Real. /a.world", "Real. ")]
    [InlineData("Real ./a.world", "Real ")]
    [InlineData("sub/Real /a.world", "sub\\Real ")]
    [InlineData("Absent /a.world", "Absent ")]
    [InlineData("Absent/Real /a.world", "Absent\\Real ")]
    [InlineData("Real.../a.world", "Real...")]
    [InlineData(".../a.world", "...")]
    [InlineData("Real\u00A0/a.world", "Real\u00A0")]
    [InlineData("Real\u3000/a.world", "Real\u3000")]
    [InlineData("Absent/Real\t/a.world", "Absent\\Real\t")]
    [InlineData("Real \\a.world", "Real ")]
    [InlineData("Real..\\a.world", "Real..")]
    [InlineData("sub\\Real \\a.world", "sub\\Real ")]
    [InlineData("Absent\\Real \\a.world", "Absent\\Real ")]
    [InlineData("Real /", "Real ")]
    [InlineData("Real \\", "Real ")]
    public void SetA1_ResolveInsideProject_RefusesASegmentEndingWithADotOrAWhiteSpace(string relativePath, string component)
    {
        bool resolved = EditorRemoteValidation.TryResolveInsideProject(_project, relativePath, out string fullPath, out string linkPath);

        Assert.False(resolved, $"'{relativePath}' was accepted as '{fullPath}'.");
        Assert.Null(fullPath);
        Assert.Equal(Path.Combine(_project, component), linkPath);
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, relativePath, out _));
    }

    // --- set A2: TryResolveAutomationPath, text only (no link) -------------------------------------------------------

    /// <summary>
    /// Set A2. The full path is '&lt;project&gt;\' followed by <paramref name="relativePath"/>; the error names the
    /// component and gives the name wording, not the link one. The tab case is already refused before the rule (its
    /// attributes cannot be read), with the link wording: the wording assertion is what fails before the rule.
    /// </summary>
    [Theory]
    [InlineData("Real /a.world", "Real ")]
    [InlineData("Real../a.world", "Real..")]
    [InlineData("Real. /a.world", "Real. ")]
    [InlineData("Real ./a.world", "Real ")]
    [InlineData("sub/Real /a.world", "sub\\Real ")]
    [InlineData("Absent /a.world", "Absent ")]
    [InlineData("Absent/Real /a.world", "Absent\\Real ")]
    [InlineData("Real.../a.world", "Real...")]
    [InlineData(".../a.world", "...")]
    [InlineData("Real\u00A0/a.world", "Real\u00A0")]
    [InlineData("Real\u3000/a.world", "Real\u3000")]
    [InlineData("Absent/Real\t/a.world", "Absent\\Real\t")]
    [InlineData("Real \\a.world", "Real ")]
    [InlineData("Real..\\a.world", "Real..")]
    [InlineData("sub\\Real \\a.world", "sub\\Real ")]
    [InlineData("Absent\\Real \\a.world", "Absent\\Real ")]
    [InlineData("Real /", "Real ")]
    [InlineData("Real \\", "Real ")]
    [InlineData("Real\t\\a.world", "Real\t")]
    public void SetA2_AutomationPath_RefusesASegmentEndingWithADotOrAWhiteSpace_WithTheNameWording(string relativePath, string component)
    {
        string input = _project + Path.DirectorySeparatorChar + relativePath;

        bool resolved = EditorRemoteValidation.TryResolveAutomationPath(_project, input, out string resolvedRelative, out string error);

        Assert.False(resolved, $"'{input}' was accepted as '{resolvedRelative}'.");
        Assert.Null(resolvedRelative);
        Assert.NotNull(error);
        Assert.Contains("'" + Path.Combine(_project, component) + "'", error);
        Assert.Contains(NameWording(component), error);
        Assert.DoesNotContain(LinkWording, error);
        Assert.DoesNotContain("a link", error);
    }

    // --- set A3: the refusal message ------------------------------------------------------------------------------------

    /// <summary>Set A3. LinkRefusal says which last character makes the name refused and names the component.</summary>
    [Theory]
    [InlineData("Real ", "ends with a space")]
    [InlineData("Real. ", "ends with a space")]
    [InlineData("Real.", "ends with a dot")]
    [InlineData("Real..", "ends with a dot")]
    [InlineData("...", "ends with a dot")]
    [InlineData("Real\t", "ends with a white-space character")]
    [InlineData("Real\u00A0", "ends with a white-space character")]
    [InlineData("Real\u3000", "ends with a white-space character")]
    public void SetA3_LinkRefusal_ForANameEndingWithADotOrAWhiteSpace_SaysSo(string componentName, string wording)
    {
        string component = Path.Combine(_project, "sub", componentName);
        string path = "sub/" + componentName + "/a.world";

        EditorRemoteArgumentException refusal = EditorRemoteValidation.LinkRefusal(path, component);

        Assert.Contains("'" + component + "'", refusal.Message);
        Assert.Contains(wording, refusal.Message);
        Assert.DoesNotContain(LinkWording, refusal.Message);
        Assert.DoesNotContain("a link", refusal.Message);
    }

    /// <summary>Set B (A3 companion). A real link keeps today's wording, both in LinkRefusal and in the automation error.</summary>
    [Fact]
    public void SetB_LinkRefusal_AndAutomationError_ForARealLink_KeepTheLinkWording()
    {
        string link = Path.Combine(_project, "linked");
        Assert.Equal(
            $"'linked/a.world' crosses '{link}', a link (or an entry that cannot be read) inside the project: paths under a link are refused (ADR-0093, A3).",
            EditorRemoteValidation.LinkRefusal("linked/a.world", link).Message);

        if (!PathSegmentJunctions.TryCreatePlain(link, _outside, _links, out string output))
        {
            _log.WriteLine($"inconclusive: junction creation failed: {output.Trim()}");
            return;
        }

        string candidate = Path.Combine(link, "a.world");
        Assert.False(EditorRemoteValidation.TryResolveAutomationPath(_project, candidate, out _, out string error));
        Assert.Equal(
            $"'{candidate}' crosses '{link}', a link (or an entry that cannot be read) inside the project folder '{_project}': paths under a link are refused (ADR-0093 P11, A3).",
            error);
    }

    // --- set B1: unchanged acceptance -----------------------------------------------------------------------------------

    /// <summary>Set B1. The rule applies to the normalized path: these are accepted before and after it.</summary>
    [Theory]
    [InlineData("a.world", "a.world")]
    [InlineData("sub/../a.world", "a.world")]
    [InlineData("Real./a.world", "Real\\a.world")]
    [InlineData("v1.2/a.world", "v1.2\\a.world")]
    [InlineData(".hidden/a.world", ".hidden\\a.world")]
    [InlineData("Real /../a.world", "a.world")]
    public void SetB1_ResolveInsideProject_AcceptsWhatNormalizesToPlainSegments(string relativePath, string normalized)
    {
        Assert.True(EditorRemoteValidation.TryResolveInsideProject(_project, relativePath, out string fullPath, out string linkPath));
        Assert.Equal(Path.Combine(_project, normalized), fullPath);
        Assert.Null(linkPath);
    }

    /// <summary>Set B1. The same paths, given to the automation resolver as full paths under the project.</summary>
    [Theory]
    [InlineData("a.world", "a.world")]
    [InlineData("sub/../a.world", "a.world")]
    [InlineData("Real./a.world", "Real\\a.world")]
    [InlineData("v1.2/a.world", "v1.2\\a.world")]
    [InlineData(".hidden/a.world", ".hidden\\a.world")]
    [InlineData("Real /../a.world", "a.world")]
    public void SetB1_AutomationPath_AcceptsWhatNormalizesToPlainSegments(string relativePath, string normalized)
    {
        string input = _project + Path.DirectorySeparatorChar + relativePath;

        Assert.True(EditorRemoteValidation.TryResolveAutomationPath(_project, input, out string resolvedRelative, out string error), error);
        Assert.Equal(normalized, resolvedRelative);
        Assert.Null(error);
    }

    // --- set B2: already refused before the rule, pinned ------------------------------------------------------------

    /// <summary>
    /// Set B2. A segment ending with a tab under an existing folder is already refused (its attributes cannot be read,
    /// fail closed) and names that component; the assertion holds before and after the rule.
    /// </summary>
    [Fact]
    public void SetB2_ResolveInsideProject_ATabEndedSegmentUnderAnExistingFolder_IsRefused()
    {
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, "Real\t/a.world", out string fullPath, out string linkPath));
        Assert.Null(fullPath);
        Assert.Equal(Path.Combine(_project, "Real\t"), linkPath);
    }

    /// <summary>Set B2, security finding S5-1. A reserved device name segment is refused (fail closed); only the refusal is pinned.</summary>
    [Theory]
    [InlineData("NUL/a.world")]
    [InlineData("nul/a.world")]
    public void SetB2_ResolveInsideProject_AReservedDeviceNameSegment_IsRefused(string relativePath)
    {
        Assert.False(EditorRemoteValidation.TryResolveInsideProject(_project, relativePath, out string fullPath, out _));
        Assert.Null(fullPath);
    }

    /// <summary>Set B2. A '\\?\' or '\\.\' spelling of a path under the project is refused as outside the project.</summary>
    [Theory]
    [InlineData(@"\\?\")]
    [InlineData(@"\\.\")]
    public void SetB2_AutomationPath_ADevicePathPrefix_IsRefused(string prefix)
    {
        string input = prefix + Path.Combine(_project, "Real ", "a.world");

        Assert.False(EditorRemoteValidation.TryResolveAutomationPath(_project, input, out string resolvedRelative, out string error));
        Assert.Null(resolvedRelative);
        Assert.NotNull(error);
    }

    // --- set A4: a junction named 'Real ' --------------------------------------------------------------------------

    /// <summary>
    /// Makes the junction '&lt;project&gt;\Real ' to the outside folder, which holds a file that exists only there; when
    /// <paramref name="realFolder"/> is false, the real 'Real' folder is removed first (the "missing component" case).
    /// False (inconclusive, written) when the junction cannot be made.
    /// </summary>
    private bool TryArrangeJunction(bool realFolder, out string onlyOutsideFile)
    {
        onlyOutsideFile = "only-outside-" + Guid.NewGuid().ToString("N")[..8] + ".world";
        File.WriteAllText(Path.Combine(_outside, onlyOutsideFile), "{\"outside\":true}");
        if (!realFolder)
        {
            Directory.Delete(Path.Combine(_project, "Real"));
        }

        if (!PathSegmentJunctions.TryCreate(_project, "Real ", _outside, _log, _links))
        {
            return false;
        }

        Assert.Equal(realFolder, PathSegmentJunctions.IsRealFolderInEnumeration(_project, "Real"));
        Assert.False(File.Exists(Path.Combine(_project, "Real", onlyOutsideFile)));

        // The access path reaches the file under the junction's target: what the link check must not let through.
        Assert.True(File.Exists(Path.Combine(_project, "Real ", onlyOutsideFile)));
        return true;
    }

    /// <summary>Set A4. TryResolveInsideProject refuses a file that exists only under the junction 'Real ', naming it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetA4_ResolveInsideProject_AFileOnlyUnderAJunctionEndingWithASpace_IsRefused(bool realFolder)
    {
        if (!TryArrangeJunction(realFolder, out string onlyOutsideFile))
        {
            return;
        }

        string[] target = PathSegmentJunctions.Snapshot(_outside);
        string relative = "Real /" + onlyOutsideFile;
        bool resolved = EditorRemoteValidation.TryResolveInsideProject(_project, relative, out string fullPath, out string linkPath);
        _log.WriteLine($"TryResolveInsideProject('{relative}'): {resolved}, full '{fullPath}', component '{linkPath}'");

        Assert.False(resolved);
        Assert.Null(fullPath);
        Assert.Equal(Path.Combine(_project, "Real "), linkPath);
        Assert.Equal(target, PathSegmentJunctions.Snapshot(_outside));
    }

    /// <summary>Set A4. TryResolveAutomationPath refuses the same file given as a full path, with the name wording.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetA4_AutomationPath_AFileOnlyUnderAJunctionEndingWithASpace_IsRefused(bool realFolder)
    {
        if (!TryArrangeJunction(realFolder, out string onlyOutsideFile))
        {
            return;
        }

        string[] target = PathSegmentJunctions.Snapshot(_outside);
        string input = Path.Combine(_project, "Real ", onlyOutsideFile);
        bool resolved = EditorRemoteValidation.TryResolveAutomationPath(_project, input, out string resolvedRelative, out string error);
        _log.WriteLine($"TryResolveAutomationPath('{input}'): {resolved}, relative '{resolvedRelative}', error: {error}");

        Assert.False(resolved);
        Assert.Null(resolvedRelative);
        Assert.NotNull(error);
        Assert.Contains("'" + Path.Combine(_project, "Real ") + "'", error);
        Assert.Contains("ends with a space", error);
        Assert.Equal(target, PathSegmentJunctions.Snapshot(_outside));
    }
}

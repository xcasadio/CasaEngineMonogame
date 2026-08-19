using CasaEngine.Editor.ContentBrowser;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public sealed class ContentBrowserConfigTests
{
    [Fact]
    public void ExcludedDirectories_DefaultsExcludeEditorAndVisualStudioFolders()
    {
        var config = new ContentBrowserConfig();

        Assert.Contains("bin", config.ExcludedDirectories);
        Assert.Contains("obj", config.ExcludedDirectories);
        Assert.Contains(".git", config.ExcludedDirectories);
        Assert.Contains(".casaeditor", config.ExcludedDirectories);
        Assert.Contains(".vs", config.ExcludedDirectories);
    }
}

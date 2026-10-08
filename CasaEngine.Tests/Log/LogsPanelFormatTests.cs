using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Log;
using MGUI.Core.UI.Text;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Log;

public class LogsPanelFormatTests
{
    /// <summary>The Logs panel draws each entry as MGUI formatted text. The verbosity brackets and the '[' of a message
    /// used to reach the parser unescaped (or doubled, which MGUI does not treat as an escape), so lines ended with a
    /// literal "[/c]" and a message such as "[EntityDetails] ..." showed as "[[EntityDetails] ...".</summary>
    [Theory]
    [InlineData(@"Load asset D:\development\repo\Content\UI/Textures/background.png")]
    [InlineData("[EntityDetails] RebuildPropertyEditors entity='EditorAutomationAnchor' component=<null>")]
    [InlineData(@"a \[ and a \[ stay as written")]
    public void FormatMarkupEntry_ShowsTheClipboardTextInTheVerbosityColour(string message)
    {
        var entry = new LogEntry(LogVerbosity.Warning, message);

        var runs = MGTextRun.ParseRuns(LogsPanel.FormatMarkupEntry(entry), new MGTextRunConfig(false))
            .OfType<MGTextRunText>()
            .ToList();

        Assert.Equal(LogsPanel.FormatClipboardEntry(entry), string.Concat(runs.Select(run => run.Text)));
        Assert.All(runs, run => Assert.Equal(Color.Yellow, run.Settings.Foreground));
    }

    /// <summary>The editor automation's <c>--logs-filter</c> names a filter of the Logs panel, case-insensitive.</summary>
    [Theory]
    [InlineData("Warning", "Warning")]
    [InlineData("warning", "Warning")]
    [InlineData("all", "All")]
    public void TryResolveFilterItem_MatchesTheFilterBoxItems(string name, string expected)
    {
        Assert.True(LogsPanel.TryResolveFilterItem(name, out var item));
        Assert.Equal(expected, item);
    }

    [Fact]
    public void TryResolveFilterItem_RejectsAnUnknownName()
    {
        Assert.False(LogsPanel.TryResolveFilterItem("Verbose", out _));
    }
}

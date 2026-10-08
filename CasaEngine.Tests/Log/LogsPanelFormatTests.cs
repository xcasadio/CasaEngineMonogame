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
}

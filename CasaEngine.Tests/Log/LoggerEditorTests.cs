using CasaEngine.Core.Logging;
using CasaEngine.Editor.Log;
using Xunit;

namespace CasaEngine.Tests.Log;

public class LoggerEditorTests
{
    [Fact]
    public void Entries_AreCappedAtMaxEntries()
    {
        var logger = new LoggerEditor { MaxEntries = 100 };

        for (int i = 0; i < 5000; i++)
        {
            logger.WriteTrace($"entry {i}");
        }

        Assert.True(logger.Entries.Count <= 100);
        Assert.NotEmpty(logger.Entries);
    }

    [Fact]
    public void Trim_DiscardsOldestEntriesFirst()
    {
        var logger = new LoggerEditor { MaxEntries = 100 };

        for (int i = 0; i < 500; i++)
        {
            logger.WriteTrace($"entry {i}");
        }

        //  The newest entry always survives; the oldest ones are the ones dropped.
        Assert.Equal("entry 499", logger.Entries[^1].Message);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message == "entry 0");
    }

    [Fact]
    public void Trim_RunsInBatches_NotOncePerEntry()
    {
        var logger = new LoggerEditor { MaxEntries = 100 };
        int trimCount = 0;
        logger.EntriesTrimmed += (_, _) => trimCount++;

        //  1000 entries over a 100-entry cap: evicting one per entry would resynchronize listeners ~900 times.
        for (int i = 0; i < 1000; i++)
        {
            logger.WriteTrace($"entry {i}");
        }

        Assert.True(trimCount > 0);
        Assert.True(trimCount <= 100, $"Expected batched trimming, got {trimCount} trims.");
    }

    [Fact]
    public void EntryAdded_IsRaisedBeforeTrim()
    {
        var logger = new LoggerEditor { MaxEntries = 10 };
        var observed = new List<string>();

        logger.EntryAdded += (_, entry) => observed.Add($"added:{entry.Message}");
        logger.EntriesTrimmed += (_, count) => observed.Add($"trimmed:{count}");

        for (int i = 0; i < 12; i++)
        {
            logger.WriteInfo($"entry {i}");
        }

        //  A listener mirroring Entries must be able to append first and resynchronize afterwards; the reverse
        //  order would make it see the newest entry twice.
        int firstTrim = observed.FindIndex(x => x.StartsWith("trimmed:"));
        Assert.True(firstTrim > 0);
        Assert.StartsWith("added:", observed[firstTrim - 1]);
    }

    [Fact]
    public void Clear_RaisesEntriesTrimmed_WithRemovedCount()
    {
        var logger = new LoggerEditor();
        logger.WriteInfo("a");
        logger.WriteInfo("b");

        int removed = 0;
        logger.EntriesTrimmed += (_, count) => removed = count;

        logger.Clear();

        Assert.Equal(2, removed);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Clear_OnEmptyLogger_DoesNotRaise()
    {
        var logger = new LoggerEditor();
        bool raised = false;
        logger.EntriesTrimmed += (_, _) => raised = true;

        logger.Clear();

        Assert.False(raised);
    }

    [Fact]
    public void LoweringMaxEntries_TrimsImmediately()
    {
        var logger = new LoggerEditor { MaxEntries = 1000 };
        for (int i = 0; i < 500; i++)
        {
            logger.WriteDebug($"entry {i}");
        }

        Assert.Equal(500, logger.Entries.Count);

        logger.MaxEntries = 50;

        Assert.True(logger.Entries.Count <= 50);
    }

    [Fact]
    public void MaxEntries_RejectsNonPositiveValues()
    {
        var logger = new LoggerEditor();

        Assert.Throws<ArgumentOutOfRangeException>(() => logger.MaxEntries = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => logger.MaxEntries = -1);
    }

    [Fact]
    public void Verbosity_IsPreservedOnEachEntry()
    {
        var logger = new LoggerEditor();

        logger.WriteTrace("t");
        logger.WriteDebug("d");
        logger.WriteInfo("i");
        logger.WriteWarning("w");
        logger.WriteError("e");

        Assert.Equal(
            new[] { LogVerbosity.Trace, LogVerbosity.Debug, LogVerbosity.Info, LogVerbosity.Warning, LogVerbosity.Error },
            logger.Entries.Select(entry => entry.Verbosity).ToArray());
    }
}

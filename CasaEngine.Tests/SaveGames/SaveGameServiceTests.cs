using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T3.1 of the save-game service plan: the public <see cref="SaveGameService"/>. Every test builds its own service
/// over a folder under <see cref="Path.GetTempPath"/>, never under the real LocalApplicationData (plan O3); the
/// <see cref="GameSettings.SaveGames"/> test only checks that nothing is resolved at construction. The class joins the
/// project-environment collection because it attaches a logger to the process-global <see cref="Logs"/> and one test
/// sets the global ProjectName.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SaveGameServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _folderPath;
    private readonly SaveGameService _service;
    private readonly CapturingLogger _logger = new();

    public SaveGameServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_SaveGameService", Guid.NewGuid().ToString("N"));
        _folderPath = Path.Combine(_rootPath, "Slots");
        _service = new SaveGameService(_folderPath);
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        // Logs has no remove method: Close detaches every logger added during the test, including this one.
        Logs.Close();

        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void Save_Then_TryLoad_RoundTripsEveryValue(SaveGameFormat format)
    {
        AllValuesSaveGameData saved = AllValuesSaveGameData.CreateFilled();

        SaveGameSaveResult saveResult = _service.Save("slot", saved, format);
        SaveGameLoadResult loadResult = _service.TryLoad("slot", out AllValuesSaveGameData loaded);

        Assert.Equal(SaveGameSaveStatus.Saved, saveResult.Status);
        Assert.True(saveResult.IsSuccess);
        Assert.Empty(saveResult.Message);
        Assert.Equal(SaveGameLoadStatus.Loaded, loadResult.Status);
        Assert.True(loadResult.IsLoaded);
        Assert.Empty(loadResult.Message);
        Assert.NotNull(loaded);
        Assert.NotSame(saved, loaded);
        saved.AssertEqual(loaded);
        Assert.True(_service.Exists("slot"));
        Assert.Empty(_logger.Warnings);
        Assert.Empty(_logger.Errors);
    }

    [Theory]
    [InlineData(SaveGameFormat.Json, (byte)'{')]
    [InlineData(SaveGameFormat.Binary, (byte)'C')]
    public void Save_WritesTheRequestedFormat(SaveGameFormat format, byte firstByte)
    {
        Assert.True(_service.Save("slot", ServiceSaveData.CreateFilled(), format).IsSuccess);

        byte[] file = File.ReadAllBytes(SlotPath("slot"));
        Assert.Equal(firstByte, file[0]);
        Assert.Equal(format, Assert.Single(_service.ListSlots()).Format);
    }

    [Fact]
    public void Save_OverwritesThePreviousSlot()
    {
        ServiceSaveData first = ServiceSaveData.CreateFilled();
        ServiceSaveData second = ServiceSaveData.CreateFilled();
        second.Hp = 7;

        Assert.True(_service.Save("slot", first, SaveGameFormat.Binary).IsSuccess);
        Assert.True(_service.Save("slot", second, SaveGameFormat.Json).IsSuccess);

        Assert.Equal(SaveGameLoadStatus.Loaded, _service.TryLoad("slot", out ServiceSaveData loaded).Status);
        Assert.Equal(7, loaded.Hp);
    }

    [Fact]
    public void TryLoad_MissingSlot_ReturnsNotFound_WithoutLogging()
    {
        SaveGameLoadResult result = _service.TryLoad("missing", out ServiceSaveData data);

        Assert.Equal(SaveGameLoadStatus.NotFound, result.Status);
        Assert.False(result.IsLoaded);
        Assert.Contains(SlotPath("missing"), result.Message);
        Assert.Null(data);
        Assert.False(_service.Exists("missing"));
        Assert.False(Directory.Exists(_folderPath));
        Assert.Empty(_logger.Warnings);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void TryLoad_FileOverTheLimit_ReturnsTooLarge()
    {
        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(SlotPath("big"), new byte[SaveGameService.MaxSlotSizeBytes + 1]);

        SaveGameLoadResult result = _service.TryLoad("big", out ServiceSaveData data);

        Assert.Equal(1024 * 1024, SaveGameService.MaxSlotSizeBytes);
        Assert.Equal(SaveGameLoadStatus.TooLarge, result.Status);
        Assert.Contains(SlotPath("big"), result.Message);
        Assert.Null(data);
        Assert.Contains(result.Message, _logger.Warnings);
    }

    [Fact]
    public void TryLoad_CorruptedBinaryFile_ReturnsCorrupted_AndLogsTheSlotPath()
    {
        Assert.True(_service.Save("slot", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        byte[] file = File.ReadAllBytes(SlotPath("slot"));
        file[file.Length / 2] ^= 0x5A;
        File.WriteAllBytes(SlotPath("slot"), file);

        SaveGameLoadResult result = _service.TryLoad("slot", out ServiceSaveData data);

        Assert.Equal(SaveGameLoadStatus.Corrupted, result.Status);
        Assert.Null(data);
        Assert.Contains(SlotPath("slot"), result.Message);
        Assert.Contains("CRC", result.Message);
        Assert.Contains(result.Message, _logger.Warnings);
    }

    [Fact]
    public void TryLoad_UnknownContainerVersion_ReturnsUnsupportedContainer()
    {
        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(SlotPath("future"), BinarySaveFileBuilder.Header(containerVersion: 2).Build());

        SaveGameLoadResult result = _service.TryLoad("future", out ServiceSaveData data);

        Assert.Equal(SaveGameLoadStatus.UnsupportedContainer, result.Status);
        Assert.Null(data);
        Assert.Contains(SlotPath("future"), result.Message);
        Assert.Contains(result.Message, _logger.Warnings);
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void TryLoad_NewerDataVersion_IsRefused_WithoutRunningSerialize(SaveGameFormat format)
    {
        Assert.True(_service.Save("slot", new ServiceSaveDataV3 { Hp = 5 }, format).IsSuccess);
        LoadCounterSaveData.LoadCalls = 0;

        SaveGameLoadResult result = _service.TryLoad("slot", out ServiceSaveData data);
        SaveGameLoadResult counted = _service.TryLoad("slot", out LoadCounterSaveData countedData);

        Assert.Equal(SaveGameLoadStatus.NewerDataVersion, result.Status);
        Assert.Null(data);
        Assert.Contains("data version 3", result.Message);
        Assert.Contains(nameof(ServiceSaveData), result.Message);
        Assert.Contains(SlotPath("slot"), result.Message);
        Assert.Contains(result.Message, _logger.Warnings);
        Assert.Equal(SaveGameLoadStatus.NewerDataVersion, counted.Status);
        Assert.Null(countedData);
        Assert.Equal(0, LoadCounterSaveData.LoadCalls);
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void TryLoad_OlderDataVersion_IsLoaded_AndSeesTheFileVersion(SaveGameFormat format)
    {
        var old = new ServiceSaveDataV1 { Hp = 12, Name = "Jess", Items = new[] { 4, 5, 6 } };
        Assert.True(_service.Save("slot", old, format).IsSuccess);

        SaveGameLoadResult result = _service.TryLoad("slot", out ServiceSaveData loaded);

        Assert.Equal(SaveGameLoadStatus.Loaded, result.Status);
        Assert.Equal(1, loaded.LoadedDataVersion);
        Assert.Equal(12, loaded.Hp);
        Assert.Equal("Jess", loaded.Name);
        Assert.Equal(new[] { 4, 5, 6 }, loaded.Items);
        Assert.Equal(ServiceSaveData.MigratedSpeed, loaded.Speed);
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void TryLoad_DataThatDoesNotMatchTheObject_ReturnsInvalidData_AndNoHalfFilledObject(SaveGameFormat format)
    {
        // Same data version, fewer fields: the load fills Hp then fails on the next field.
        var partial = new DelegateSaveGameData(
            archive =>
            {
                int hp = 99;
                archive.Value("hp", ref hp);
            },
            ServiceSaveData.Latest);
        Assert.True(_service.Save("slot", partial, format).IsSuccess);

        SaveGameLoadResult result = _service.TryLoad("slot", out ServiceSaveData data);

        Assert.Equal(SaveGameLoadStatus.InvalidData, result.Status);
        Assert.Null(data);
        Assert.Contains(SlotPath("slot"), result.Message);
        Assert.Contains("name", result.Message);
        Assert.Contains(result.Message, _logger.Warnings);
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsInvalidData()
    {
        Directory.CreateDirectory(_folderPath);
        File.WriteAllText(SlotPath("slot"), "{ \"container\": ");

        SaveGameLoadResult result = _service.TryLoad("slot", out ServiceSaveData data);

        Assert.Equal(SaveGameLoadStatus.InvalidData, result.Status);
        Assert.Null(data);
        Assert.Contains(SlotPath("slot"), result.Message);
    }

    [Fact]
    public void EveryResultOtherThanLoaded_LeavesDataDefault()
    {
        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(SlotPath("too-large"), new byte[SaveGameService.MaxSlotSizeBytes + 1]);
        Assert.True(_service.Save("corrupted", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        byte[] corrupted = File.ReadAllBytes(SlotPath("corrupted"));
        corrupted[^1] ^= 0xFF;
        File.WriteAllBytes(SlotPath("corrupted"), corrupted);
        File.WriteAllBytes(SlotPath("unsupported"), BinarySaveFileBuilder.Header(containerVersion: 9).Build());
        Assert.True(_service.Save("newer", new ServiceSaveDataV3(), SaveGameFormat.Binary).IsSuccess);
        Assert.True(_service.Save("invalid", new DelegateSaveGameData(_ => { }, ServiceSaveData.Latest), SaveGameFormat.Json).IsSuccess);
        Assert.True(_service.Save("locked", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);

        var statuses = new HashSet<SaveGameLoadStatus>();
        void AssertDefault(string slot)
        {
            SaveGameLoadResult result = _service.TryLoad(slot, out ServiceSaveData data);
            Assert.NotEqual(SaveGameLoadStatus.Loaded, result.Status);
            Assert.Null(data);
            Assert.False(string.IsNullOrEmpty(result.Message));
            statuses.Add(result.Status);
        }

        AssertDefault("missing");
        AssertDefault("too-large");
        AssertDefault("corrupted");
        AssertDefault("unsupported");
        AssertDefault("newer");
        AssertDefault("invalid");
        using (LockSlot("locked"))
        {
            AssertDefault("locked");
        }

        SaveGameLoadStatus[] everyFailure = Enum.GetValues<SaveGameLoadStatus>().Where(s => s != SaveGameLoadStatus.Loaded).ToArray();
        Assert.Equal(everyFailure.OrderBy(s => s), statuses.OrderBy(s => s));
    }

    [Fact]
    public void LockedSlot_SaveTryLoadAndDelete_ReturnIoError_AndKeepPreviousContent()
    {
        Assert.True(_service.Save("slot", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        byte[] previous = File.ReadAllBytes(SlotPath("slot"));

        SaveGameSaveResult save;
        SaveGameLoadResult load;
        SaveGameSaveResult delete;
        ServiceSaveData data;
        using (LockSlot("slot"))
        {
            save = _service.Save("slot", new ServiceSaveData { Hp = 1 }, SaveGameFormat.Json);
            load = _service.TryLoad("slot", out data);
            delete = _service.Delete("slot");
        }

        Assert.Equal(SaveGameSaveStatus.IoError, save.Status);
        Assert.False(save.IsSuccess);
        Assert.Contains(SlotPath("slot"), save.Message);
        Assert.Equal(SaveGameLoadStatus.IoError, load.Status);
        Assert.Contains(SlotPath("slot"), load.Message);
        Assert.Null(data);
        Assert.Equal(SaveGameSaveStatus.IoError, delete.Status);
        Assert.Contains(SlotPath("slot"), delete.Message);
        Assert.Contains(save.Message, _logger.Errors);
        Assert.Contains(load.Message, _logger.Errors);
        Assert.Contains(delete.Message, _logger.Errors);
        Assert.Equal(previous, File.ReadAllBytes(SlotPath("slot")));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void Save_AboveTheSlotLimit_ReturnsTooLarge_AndWritesNothing(SaveGameFormat format)
    {
        var big = new DelegateSaveGameData(archive =>
        {
            string text = new('x', SaveGameService.MaxSlotSizeBytes + 16);
            archive.Value("text", ref text);
        });

        SaveGameSaveResult result = _service.Save("big", big, format);

        Assert.Equal(SaveGameSaveStatus.TooLarge, result.Status);
        Assert.Contains(SlotPath("big"), result.Message);
        Assert.Contains(result.Message, _logger.Errors);
        Assert.False(_service.Exists("big"));
        Assert.False(Directory.Exists(_folderPath));
    }

    [Fact]
    public void Save_AboveTheSlotLimit_KeepsThePreviousSlot()
    {
        Assert.True(_service.Save("slot", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        byte[] previous = File.ReadAllBytes(SlotPath("slot"));
        var big = new DelegateSaveGameData(archive =>
        {
            var bytes = new byte[SaveGameService.MaxSlotSizeBytes];
            archive.Value("bytes", bytes);
        });

        SaveGameSaveResult result = _service.Save("slot", big, SaveGameFormat.Binary);

        Assert.Equal(SaveGameSaveStatus.TooLarge, result.Status);
        Assert.Equal(previous, File.ReadAllBytes(SlotPath("slot")));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Fact]
    public void Save_ExactlyAtTheSlotLimit_IsSaved_AndLoads()
    {
        // Measure the envelope around an empty string once, then fill the file to exactly the limit.
        Assert.True(_service.Save("probe", new StringSaveData(), SaveGameFormat.Binary).IsSuccess);
        long overhead = new FileInfo(SlotPath("probe")).Length;
        var exact = new StringSaveData { Text = new string('y', SaveGameService.MaxSlotSizeBytes - (int)overhead) };

        SaveGameSaveResult result = _service.Save("exact", exact, SaveGameFormat.Binary);

        Assert.Equal(SaveGameSaveStatus.Saved, result.Status);
        Assert.Equal(SaveGameService.MaxSlotSizeBytes, new FileInfo(SlotPath("exact")).Length);
        Assert.Equal(SaveGameLoadStatus.Loaded, _service.TryLoad("exact", out StringSaveData loaded).Status);
        Assert.Equal(exact.Text, loaded.Text);
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void Save_RefusedValue_ReturnsInvalidData_AndWritesNothing(SaveGameFormat format)
    {
        var nonFinite = new DelegateSaveGameData(archive =>
        {
            float speed = float.NaN;
            archive.Value("speed", ref speed);
        });

        SaveGameSaveResult result = _service.Save("slot", nonFinite, format);
        SaveGameSaveResult nullMetadata = _service.Save(
            "slot",
            ServiceSaveData.CreateFilled(),
            format,
            new Dictionary<string, string> { ["location"] = null });

        Assert.Equal(SaveGameSaveStatus.InvalidData, result.Status);
        Assert.Contains("speed", result.Message);
        Assert.Contains(SlotPath("slot"), result.Message);
        Assert.Contains(result.Message, _logger.Errors);
        Assert.Equal(SaveGameSaveStatus.InvalidData, nullMetadata.Status);
        Assert.Contains("location", nullMetadata.Message);
        Assert.False(_service.Exists("slot"));
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void Save_SerializeThrowing_Propagates_AndWritesNothing(SaveGameFormat format)
    {
        Assert.Throws<InvalidOperationException>(() => _service.Save("slot", new ThrowingSaveData { ThrowWhenSaving = true }, format));

        Assert.False(_service.Exists("slot"));
        Assert.False(Directory.Exists(_folderPath));
    }

    [Theory]
    [InlineData(SaveGameFormat.Json)]
    [InlineData(SaveGameFormat.Binary)]
    public void TryLoad_SerializeThrowing_Propagates(SaveGameFormat format)
    {
        Assert.True(_service.Save("slot", new ThrowingSaveData(), format).IsSuccess);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _service.TryLoad("slot", out ThrowingSaveData _));

        Assert.Equal(ThrowingSaveData.LoadMessage, exception.Message);
    }

    [Fact]
    public void DeveloperMisuse_Throws()
    {
        ServiceSaveData data = ServiceSaveData.CreateFilled();
        var unbalanced = new DelegateSaveGameData(archive => archive.BeginObject("open"));
        var duplicateName = new DelegateSaveGameData(archive =>
        {
            int value = 1;
            archive.Value("same", ref value);
            archive.Value("same", ref value);
        });

        foreach (string slot in new[] { null, "", "Slot", "../slot", "con", new string('a', 33) })
        {
            Assert.ThrowsAny<ArgumentException>(() => _service.Save(slot, data, SaveGameFormat.Binary));
            Assert.ThrowsAny<ArgumentException>(() => _service.TryLoad(slot, out ServiceSaveData _));
            Assert.ThrowsAny<ArgumentException>(() => _service.Exists(slot));
            Assert.ThrowsAny<ArgumentException>(() => _service.Delete(slot));
        }

        Assert.Throws<ArgumentNullException>(() => _service.Save("slot", null, SaveGameFormat.Binary));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Save("slot", data, (SaveGameFormat)42));
        Assert.Throws<InvalidOperationException>(() => _service.Save("slot", unbalanced, SaveGameFormat.Json));
        Assert.Throws<InvalidOperationException>(() => _service.Save("slot", unbalanced, SaveGameFormat.Binary));
        Assert.Throws<ArgumentException>(() => _service.Save("slot", duplicateName, SaveGameFormat.Json));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Save("slot", new DelegateSaveGameData(_ => { }, -1), SaveGameFormat.Binary));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.TryLoad("slot", out NegativeVersionSaveData _));
        Assert.False(Directory.Exists(_folderPath));
    }

    [Fact]
    public void TryLoad_UnbalancedObjectsWhileLoading_Propagates()
    {
        Assert.True(_service.Save("slot", new UnbalancedWhenLoadingSaveData(), SaveGameFormat.Json).IsSuccess);

        Assert.Throws<InvalidOperationException>(() => _service.TryLoad("slot", out UnbalancedWhenLoadingSaveData _));
    }

    [Fact]
    public void Delete_RemovesTheSlot_ThenReportsNotFound()
    {
        Assert.True(_service.Save("slot", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);

        SaveGameSaveResult deleted = _service.Delete("slot");
        SaveGameSaveResult again = _service.Delete("slot");

        Assert.Equal(SaveGameSaveStatus.Deleted, deleted.Status);
        Assert.True(deleted.IsSuccess);
        Assert.False(_service.Exists("slot"));
        Assert.Equal(SaveGameSaveStatus.NotFound, again.Status);
        Assert.False(again.IsSuccess);
        Assert.Contains(SlotPath("slot"), again.Message);
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public void ListSlots_ReturnsEverySlotWithItsHeaderAndMetadata()
    {
        var metadataA = new Dictionary<string, string> { ["location"] = "Inoa", ["playTime"] = "01:23:45" };
        var metadataB = new Dictionary<string, string> { ["location"] = "Murgg <c=red>woods</c>" };
        Assert.True(_service.Save("slot-b", new ServiceSaveDataV1(), SaveGameFormat.Json, metadataB).IsSuccess);
        Assert.True(_service.Save("slot-a", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary, metadataA).IsSuccess);
        Assert.True(_service.Save("slot-c", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        var time = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(SlotPath("slot-a"), time);

        IReadOnlyList<SaveGameSlotInfo> slots = _service.ListSlots();

        Assert.Equal(new[] { "slot-a", "slot-b", "slot-c" }, slots.Select(s => s.Name));
        Assert.All(slots, s => Assert.True(s.IsReadable));
        Assert.All(slots, s => Assert.Equal(SaveGameLoadStatus.Loaded, s.Status));
        Assert.All(slots, s => Assert.Empty(s.Message));

        SaveGameSlotInfo a = slots[0];
        Assert.Equal(SaveGameFormat.Binary, a.Format);
        Assert.Equal(ServiceSaveData.Latest, a.DataVersion);
        Assert.Equal(metadataA.OrderBy(p => p.Key, StringComparer.Ordinal), a.Metadata.OrderBy(p => p.Key, StringComparer.Ordinal));
        Assert.Equal(time, a.LastWriteTimeUtc);

        SaveGameSlotInfo b = slots[1];
        Assert.Equal(SaveGameFormat.Json, b.Format);
        Assert.Equal(1, b.DataVersion);
        Assert.Equal("Murgg <c=red>woods</c>", b.Metadata["location"]);
        Assert.Equal(File.GetLastWriteTimeUtc(SlotPath("slot-b")), b.LastWriteTimeUtc);

        Assert.Empty(slots[2].Metadata);

        // The listing is a snapshot: neither the list nor a slot's metadata can be changed through it.
        Assert.True(((ICollection<SaveGameSlotInfo>)slots).IsReadOnly);
        Assert.True(((IDictionary<string, string>)a.Metadata).IsReadOnly);
    }

    [Fact]
    public void ListSlots_FlagsCorruptedAndTooLargeSlots_AndListsTheHealthyOnes()
    {
        Assert.True(_service.Save("healthy-1", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary, new Dictionary<string, string> { ["n"] = "1" }).IsSuccess);
        Assert.True(_service.Save("healthy-2", ServiceSaveData.CreateFilled(), SaveGameFormat.Json, new Dictionary<string, string> { ["n"] = "2" }).IsSuccess);
        Assert.True(_service.Save("corrupted", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        byte[] corrupted = File.ReadAllBytes(SlotPath("corrupted"));
        corrupted[corrupted.Length / 2] ^= 0x01;
        File.WriteAllBytes(SlotPath("corrupted"), corrupted);
        File.WriteAllBytes(SlotPath("too-large"), new byte[SaveGameService.MaxSlotSizeBytes + 1]);
        File.WriteAllText(SlotPath("not-json"), "{ broken");

        IReadOnlyList<SaveGameSlotInfo> slots = _service.ListSlots();

        Assert.Equal(new[] { "corrupted", "healthy-1", "healthy-2", "not-json", "too-large" }, slots.Select(s => s.Name));
        Dictionary<string, SaveGameSlotInfo> byName = slots.ToDictionary(s => s.Name);

        Assert.True(byName["healthy-1"].IsReadable);
        Assert.Equal("1", byName["healthy-1"].Metadata["n"]);
        Assert.True(byName["healthy-2"].IsReadable);
        Assert.Equal("2", byName["healthy-2"].Metadata["n"]);

        AssertUnreadable(byName["corrupted"], SaveGameLoadStatus.Corrupted);
        AssertUnreadable(byName["too-large"], SaveGameLoadStatus.TooLarge);
        AssertUnreadable(byName["not-json"], SaveGameLoadStatus.InvalidData);
        Assert.Contains(byName["corrupted"].Message, _logger.Warnings);
        Assert.Contains(byName["too-large"].Message, _logger.Warnings);
        Assert.Contains(byName["not-json"].Message, _logger.Warnings);
    }

    [Fact]
    public void ListSlots_LockedSlot_IsFlaggedAsIoError()
    {
        Assert.True(_service.Save("healthy", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);
        Assert.True(_service.Save("locked", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);

        IReadOnlyList<SaveGameSlotInfo> slots;
        using (LockSlot("locked"))
        {
            slots = _service.ListSlots();
        }

        Assert.Equal(new[] { "healthy", "locked" }, slots.Select(s => s.Name));
        Assert.True(slots[0].IsReadable);
        AssertUnreadable(slots[1], SaveGameLoadStatus.IoError, expectLastWriteTime: false);
        Assert.Contains(slots[1].Message, _logger.Errors);

        // The locked file cannot be opened to read its time either: that failure is logged, the time is null.
        Assert.Null(slots[1].LastWriteTimeUtc);
        Assert.Contains(_logger.Errors, e => e.Contains("last-write time read") && e.Contains(SlotPath("locked")));
    }

    [Fact]
    public void ListSlots_MissingFolder_IsEmpty_AndNotCreated()
    {
        Assert.Empty(_service.ListSlots());
        Assert.False(Directory.Exists(_folderPath));
    }

    [Fact]
    public void GameSettingsSaveGames_ConstructionTouchesNoDisk_TheFolderResolvesAtTheFirstCall()
    {
        string previousProjectName = GameSettings.ProjectSettings.ProjectName;
        try
        {
            // With the default ProjectName the folder cannot be resolved: constructing the service must not try.
            GameSettings.ProjectSettings.ProjectName = new ProjectSettings().ProjectName;

            var service = new SaveGameService();
            SaveGameService engineService = GameSettings.SaveGames;

            Assert.NotNull(engineService);
            Assert.Same(engineService, GameSettings.SaveGames);

            // The first I/O call resolves the folder and refuses the undefined project name, before any disk access.
            Assert.Throws<InvalidOperationException>(() => service.Exists("slot"));
            Assert.Throws<InvalidOperationException>(() => service.ListSlots());
            Assert.Throws<InvalidOperationException>(() => engineService.Exists("slot"));
        }
        finally
        {
            GameSettings.ProjectSettings.ProjectName = previousProjectName;
        }
    }

    [Fact]
    public void DefaultFolder_IsResolvedAtTheFirstCall_UnderTheInjectedRoot()
    {
        string localApplicationData = Path.Combine(_rootPath, "LocalAppData");
        string projectName = new ProjectSettings().ProjectName;
        var service = new SaveGameService(new SaveGameFileStorage(() => localApplicationData, () => projectName));
        projectName = "AlundraGame";

        Assert.True(service.Save("slot", ServiceSaveData.CreateFilled(), SaveGameFormat.Binary).IsSuccess);

        Assert.True(File.Exists(Path.Combine(localApplicationData, "AlundraGame", "SaveGames", "slot.sav")));
    }

    private static void AssertUnreadable(SaveGameSlotInfo slot, SaveGameLoadStatus expected, bool expectLastWriteTime = true)
    {
        Assert.False(slot.IsReadable);
        Assert.Equal(expected, slot.Status);
        Assert.False(string.IsNullOrEmpty(slot.Message));
        Assert.Contains(slot.Name + ".sav", slot.Message);
        Assert.Empty(slot.Metadata);
        Assert.Equal(0, slot.DataVersion);
        if (expectLastWriteTime)
        {
            Assert.NotNull(slot.LastWriteTimeUtc);
        }
    }

    private string SlotPath(string slot)
    {
        return Path.Combine(_folderPath, slot + ".sav");
    }

    private FileStream LockSlot(string slot)
    {
        return new FileStream(SlotPath(slot), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) => Errors.Add(msg);
    }
}

/// <summary>The current layout (version 2): <see cref="Speed"/> was added in version 2.</summary>
internal sealed class ServiceSaveData : ISaveGameData
{
    public const int Latest = 2;
    public const float MigratedSpeed = 1.5f;

    public int Hp;
    public string Name = string.Empty;
    public int[] Items = new int[3];
    public float Speed;

    /// <summary>The archive's data version seen by the last load.</summary>
    public int LoadedDataVersion = -1;

    public int LatestDataVersion => Latest;

    public static ServiceSaveData CreateFilled()
    {
        return new ServiceSaveData { Hp = 30, Name = "Alundra", Items = new[] { 1, 2, 3 }, Speed = 2.25f };
    }

    public void Serialize(SaveGameArchive archive)
    {
        if (archive.IsLoading)
        {
            LoadedDataVersion = archive.DataVersion;
        }

        archive.Value("hp", ref Hp);
        archive.Value("name", ref Name);
        archive.Value("items", Items);
        if (archive.DataVersion >= 2)
        {
            archive.Value("speed", ref Speed);
        }
        else
        {
            Speed = MigratedSpeed;
        }
    }
}

/// <summary>The same object as an older build wrote it (version 1, without speed).</summary>
internal sealed class ServiceSaveDataV1 : ISaveGameData
{
    public int Hp;
    public string Name = string.Empty;
    public int[] Items = new int[3];

    public int LatestDataVersion => 1;

    public void Serialize(SaveGameArchive archive)
    {
        archive.Value("hp", ref Hp);
        archive.Value("name", ref Name);
        archive.Value("items", Items);
    }
}

/// <summary>A newer build's object (version 3).</summary>
internal sealed class ServiceSaveDataV3 : ISaveGameData
{
    public int Hp;

    public int LatestDataVersion => 3;

    public void Serialize(SaveGameArchive archive)
    {
        archive.Value("hp", ref Hp);
    }
}

/// <summary>Counts its loading Serialize calls: a refused newer version must not run them.</summary>
internal sealed class LoadCounterSaveData : ISaveGameData
{
    public static int LoadCalls;

    public int LatestDataVersion => ServiceSaveData.Latest;

    public void Serialize(SaveGameArchive archive)
    {
        if (archive.IsLoading)
        {
            LoadCalls++;
        }
    }
}

internal sealed class StringSaveData : ISaveGameData
{
    public string Text = string.Empty;

    public int LatestDataVersion => 1;

    public void Serialize(SaveGameArchive archive)
    {
        archive.Value("text", ref Text);
    }
}

/// <summary>A game bug: Serialize throws on its own, when saving or when loading.</summary>
internal sealed class ThrowingSaveData : ISaveGameData
{
    public const string LoadMessage = "Game bug while loading.";

    public bool ThrowWhenSaving;
    public int Hp = 4;

    public int LatestDataVersion => 1;

    public void Serialize(SaveGameArchive archive)
    {
        if (!archive.IsLoading && ThrowWhenSaving)
        {
            throw new InvalidOperationException("Game bug while saving.");
        }

        archive.Value("hp", ref Hp);
        if (archive.IsLoading)
        {
            throw new InvalidOperationException(LoadMessage);
        }
    }
}

internal sealed class NegativeVersionSaveData : ISaveGameData
{
    public int LatestDataVersion => -1;

    public void Serialize(SaveGameArchive archive)
    {
    }
}

/// <summary>Balanced when saving, leaves an object open when loading: a developer error on the load path.</summary>
internal sealed class UnbalancedWhenLoadingSaveData : ISaveGameData
{
    public int LatestDataVersion => 1;

    public void Serialize(SaveGameArchive archive)
    {
        archive.BeginObject("player");
        if (!archive.IsLoading)
        {
            archive.EndObject();
        }
    }
}

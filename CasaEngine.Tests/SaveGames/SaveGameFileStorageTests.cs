using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T1.1 of the save-game service plan. Every test writes under <see cref="Path.GetTempPath"/>, never under the
/// real LocalApplicationData: the default folder is exercised through the internal provider constructor rooted in
/// a temporary folder, or through <see cref="SaveGameFileStorage.ResolveFolderPath"/>, which touches no disk.
/// The class joins the project-environment collection because one test sets the global ProjectName.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SaveGameFileStorageTests : IDisposable
{
    private readonly string _rootPath;
    private readonly string _folderPath;
    private readonly SaveGameFileStorage _storage;

    public SaveGameFileStorageTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame_SaveGames", Guid.NewGuid().ToString("N"));
        _folderPath = Path.Combine(_rootPath, "Slots");
        _storage = new SaveGameFileStorage(_folderPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }
    }

    [Fact]
    public void Write_Then_TryRead_RoundTripsBytes()
    {
        byte[] content = { 0, 1, 2, 250, 255, 42 };

        SaveGameStorageResult writeResult = _storage.Write("slot-1", content);
        SaveGameStorageResult readResult = _storage.TryRead("slot-1", out byte[] data);

        Assert.Equal(SaveGameStorageStatus.Success, writeResult.Status);
        Assert.Equal(SaveGameStorageStatus.Success, readResult.Status);
        Assert.Equal(content, data);
        Assert.True(_storage.Exists("slot-1"));
        Assert.True(File.Exists(Path.Combine(_folderPath, "slot-1.sav")));
    }

    [Fact]
    public void Write_OverwritesPreviousContent()
    {
        Assert.True(_storage.Write("slot", new byte[] { 1, 2, 3, 4, 5 }).IsSuccess);
        Assert.True(_storage.Write("slot", new byte[] { 9 }).IsSuccess);

        Assert.Equal(SaveGameStorageStatus.Success, _storage.TryRead("slot", out byte[] data).Status);
        Assert.Equal(new byte[] { 9 }, data);
    }

    [Fact]
    public void EnumerateSlots_ReturnsSlotsInOrdinalOrder()
    {
        foreach (string slot in new[] { "b", "a_1", "10", "a", "9", "a-2" })
        {
            Assert.True(_storage.Write(slot, new byte[] { 1 }).IsSuccess);
        }

        SaveGameStorageResult result = _storage.EnumerateSlots(out IReadOnlyList<string> slots);

        Assert.Equal(SaveGameStorageStatus.Success, result.Status);
        Assert.Equal(new[] { "10", "9", "a", "a-2", "a_1", "b" }, slots);
        _storage.EnumerateSlots(out IReadOnlyList<string> again);
        Assert.Equal(slots, again);
    }

    [Fact]
    public void Folder_IsCreatedOnFirstWriteOnly()
    {
        Assert.False(_storage.Exists("slot"));
        Assert.Equal(SaveGameStorageStatus.NotFound, _storage.TryRead("slot", out byte[] data).Status);
        Assert.Null(data);
        Assert.Equal(SaveGameStorageStatus.NotFound, _storage.Delete("slot").Status);
        Assert.Equal(SaveGameStorageStatus.Success, _storage.EnumerateSlots(out IReadOnlyList<string> slots).Status);
        Assert.Empty(slots);
        Assert.False(Directory.Exists(_folderPath));

        Assert.True(_storage.Write("slot", new byte[] { 7 }).IsSuccess);

        Assert.True(Directory.Exists(_folderPath));
    }

    [Fact]
    public void Delete_RemovesTheSlot()
    {
        Assert.True(_storage.Write("slot", new byte[] { 7 }).IsSuccess);

        Assert.Equal(SaveGameStorageStatus.Success, _storage.Delete("slot").Status);

        Assert.False(_storage.Exists("slot"));
        Assert.Equal(SaveGameStorageStatus.NotFound, _storage.Delete("slot").Status);
    }

    public static IEnumerable<object[]> RefusedSlotNames()
    {
        yield return new object[] { null };
        yield return new object[] { "" };
        yield return new object[] { "a/b" };
        yield return new object[] { "a\\b" };
        yield return new object[] { ".." };
        yield return new object[] { "../slot" };
        yield return new object[] { new string('a', 33) };
        yield return new object[] { "Slot" };
        yield return new object[] { "slot:stream" };
        yield return new object[] { "slot." };
        yield return new object[] { "slot " };
        yield return new object[] { "a<b" };
        yield return new object[] { "a>b" };
        yield return new object[] { "a\"b" };
        yield return new object[] { "a|b" };
        yield return new object[] { "a?b" };
        yield return new object[] { "a*b" };
        yield return new object[] { "a\u0001b" };
        yield return new object[] { "slot\n" };
        yield return new object[] { "slét" };
        yield return new object[] { "con" };
        yield return new object[] { "nul" };
        yield return new object[] { "com1" };
        yield return new object[] { "lpt9" };
    }

    [Theory]
    [MemberData(nameof(RefusedSlotNames))]
    public void RefusedSlotName_ThrowsArgumentException_ForEveryOperation(string slotName)
    {
        Assert.False(SaveGameNames.IsValidSlotName(slotName));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Exists(slotName));
        Assert.ThrowsAny<ArgumentException>(() => _storage.TryRead(slotName, out _));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Write(slotName, new byte[] { 1 }));
        Assert.ThrowsAny<ArgumentException>(() => _storage.Delete(slotName));
        Assert.False(Directory.Exists(_folderPath));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("slot_1")]
    [InlineData("auto-save")]
    [InlineData("com10")]
    [InlineData("console")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")]
    public void AcceptedSlotName_IsValid(string slotName)
    {
        Assert.True(SaveGameNames.IsValidSlotName(slotName));
    }

    [Theory]
    [InlineData("AlundraGame")]
    [InlineData("My Game 2")]
    [InlineData("a")]
    [InlineData("Game_")]
    [InlineData("Game-")]
    public void AcceptedProjectFolderName_IsValid(string projectName)
    {
        Assert.True(SaveGameNames.IsValidProjectFolderName(projectName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Game")]
    [InlineData("_Game")]
    [InlineData("Game ")]
    [InlineData("Game.")]
    [InlineData("Game\n")]
    [InlineData("CON")]
    [InlineData("Nul")]
    [InlineData("com9")]
    [InlineData("LPT1")]
    [InlineData("con.txt")]
    public void RefusedProjectFolderName_IsInvalid(string projectName)
    {
        Assert.False(SaveGameNames.IsValidProjectFolderName(projectName));
    }

    [Fact]
    public void ProjectFolderName_LengthIsCappedAt64()
    {
        Assert.True(SaveGameNames.IsValidProjectFolderName(new string('a', 64)));
        Assert.False(SaveGameNames.IsValidProjectFolderName(new string('a', 65)));
        Assert.False(SaveGameNames.IsValidProjectFolderName(null));
    }

    [Fact]
    public void Write_LeavesNoTemporaryFile()
    {
        Assert.True(_storage.Write("slot", new byte[] { 1, 2, 3 }).IsSuccess);
        Assert.True(_storage.Write("slot", new byte[] { 4, 5 }).IsSuccess);

        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
        Assert.Equal(new[] { "slot.sav" }, Directory.GetFiles(_folderPath).Select(Path.GetFileName));
    }

    [Fact]
    public void Write_SourceThrowsMidway_KeepsPreviousContentAndLeavesNoTemporaryFile()
    {
        byte[] previous = CreateContent(4096, seed: 3);
        Assert.True(_storage.Write("slot", previous).IsSuccess);

        Assert.Throws<InvalidOperationException>(() => _storage.Write("slot", stream =>
        {
            stream.Write(CreateContent(2048, seed: 9), 0, 2048);
            throw new InvalidOperationException("The game's serializer failed half-way.");
        }));

        Assert.Equal(previous, File.ReadAllBytes(Path.Combine(_folderPath, "slot.sav")));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Fact]
    public void Write_SourceFailsWithIoErrorMidway_ReturnsIoErrorAndKeepsPreviousContent()
    {
        byte[] previous = CreateContent(4096, seed: 5);
        Assert.True(_storage.Write("slot", previous).IsSuccess);

        SaveGameStorageResult result = _storage.Write("slot", stream =>
        {
            stream.Write(CreateContent(2048, seed: 11), 0, 2048);
            throw new IOException("Simulated disk full.");
        });

        Assert.Equal(SaveGameStorageStatus.IoError, result.Status);
        Assert.Contains("write", result.Message);
        Assert.Contains(Path.Combine(_folderPath, "slot.sav"), result.Message);
        Assert.Equal(previous, File.ReadAllBytes(Path.Combine(_folderPath, "slot.sav")));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Fact]
    public void OrphanTemporaryFile_DoesNotBlockWrite_StaysIntact_AndIsNeverASlot()
    {
        Directory.CreateDirectory(_folderPath);
        string orphanPath = Path.Combine(_folderPath, "slot." + Guid.NewGuid().ToString("N") + ".tmp");
        string ghostPath = Path.Combine(_folderPath, "ghost." + Guid.NewGuid().ToString("N") + ".tmp");
        byte[] orphanContent = CreateContent(64, seed: 1);
        File.WriteAllBytes(orphanPath, orphanContent);
        File.WriteAllBytes(ghostPath, orphanContent);

        Assert.True(_storage.Write("slot", new byte[] { 1, 2 }).IsSuccess);

        Assert.Equal(orphanContent, File.ReadAllBytes(orphanPath));
        Assert.Equal(orphanContent, File.ReadAllBytes(ghostPath));
        Assert.False(_storage.Exists("ghost"));
        Assert.Equal(SaveGameStorageStatus.NotFound, _storage.TryRead("ghost", out _).Status);
        _storage.EnumerateSlots(out IReadOnlyList<string> slots);
        Assert.Equal(new[] { "slot" }, slots);
    }

    [Fact]
    public void EnumerateSlots_IgnoresHandPlacedInvalidNamesAndExtensions()
    {
        Directory.CreateDirectory(_folderPath);
        foreach (string fileName in new[]
                 {
                     "valid.sav", "Upper.sav", "bad name.sav", "upper.SAV", "other.Sav", "slot.sav.bak",
                     "slot.savx", "slot.sav.tmp", new string('a', 33) + ".sav", "noextension", ".sav",
                 })
        {
            File.WriteAllBytes(Path.Combine(_folderPath, fileName), new byte[] { 1 });
        }

        Directory.CreateDirectory(Path.Combine(_folderPath, "folder.sav"));

        Assert.Equal(SaveGameStorageStatus.Success, _storage.EnumerateSlots(out IReadOnlyList<string> slots).Status);
        Assert.Equal(new[] { "valid" }, slots);
    }

    [Fact]
    public void TryRead_FileOneByteOverTheLimit_ReturnsTooLarge()
    {
        Directory.CreateDirectory(_folderPath);
        File.WriteAllBytes(Path.Combine(_folderPath, "big.sav"), new byte[SaveGameFileStorage.MaxSlotSizeBytes + 1]);
        File.WriteAllBytes(Path.Combine(_folderPath, "edge.sav"), new byte[SaveGameFileStorage.MaxSlotSizeBytes]);

        SaveGameStorageResult big = _storage.TryRead("big", out byte[] bigData);
        SaveGameStorageResult edge = _storage.TryRead("edge", out byte[] edgeData);

        Assert.Equal(1024 * 1024, SaveGameFileStorage.MaxSlotSizeBytes);
        Assert.Equal(SaveGameStorageStatus.TooLarge, big.Status);
        Assert.Null(bigData);
        Assert.Equal(SaveGameStorageStatus.Success, edge.Status);
        Assert.Equal(SaveGameFileStorage.MaxSlotSizeBytes, edgeData.Length);
    }

    [Fact]
    public void LockedSlot_ReadWriteAndDelete_ReturnIoError_AndKeepPreviousContent()
    {
        byte[] previous = CreateContent(512, seed: 7);
        Assert.True(_storage.Write("slot", previous).IsSuccess);
        string slotPath = Path.Combine(_folderPath, "slot.sav");

        SaveGameStorageResult read;
        SaveGameStorageResult write;
        SaveGameStorageResult delete;
        byte[] data;
        using (new FileStream(slotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            read = _storage.TryRead("slot", out data);
            write = _storage.Write("slot", new byte[] { 1 });
            delete = _storage.Delete("slot");
        }

        Assert.Equal(SaveGameStorageStatus.IoError, read.Status);
        Assert.Contains("read", read.Message);
        Assert.Contains(slotPath, read.Message);
        Assert.Null(data);
        Assert.Equal(SaveGameStorageStatus.IoError, write.Status);
        Assert.Contains("write", write.Message);
        Assert.Equal(SaveGameStorageStatus.IoError, delete.Status);
        Assert.Contains("delete", delete.Message);
        Assert.Equal(previous, File.ReadAllBytes(slotPath));
        Assert.Empty(Directory.GetFiles(_folderPath, "*.tmp"));
    }

    [Fact]
    public void TryGetLastWriteTimeUtc_ReturnsTheFileTime_OrNotFound()
    {
        Assert.Equal(SaveGameStorageStatus.NotFound, _storage.TryGetLastWriteTimeUtc("slot", out DateTime missingTime).Status);
        Assert.Equal(default, missingTime);
        Assert.False(Directory.Exists(_folderPath));

        Assert.True(_storage.Write("slot", new byte[] { 1 }).IsSuccess);
        string slotPath = Path.Combine(_folderPath, "slot.sav");
        var expected = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(slotPath, expected);

        SaveGameStorageResult result = _storage.TryGetLastWriteTimeUtc("slot", out DateTime lastWriteTimeUtc);

        Assert.Equal(SaveGameStorageStatus.Success, result.Status);
        Assert.Equal(expected, lastWriteTimeUtc);
        Assert.Equal(DateTimeKind.Utc, lastWriteTimeUtc.Kind);
        Assert.Throws<ArgumentException>(() => _storage.TryGetLastWriteTimeUtc("../slot", out _));
    }

    [Fact]
    public void TryGetLastWriteTimeUtc_LockedSlot_ReturnsIoError_WithoutException()
    {
        Assert.True(_storage.Write("slot", new byte[] { 1 }).IsSuccess);
        string slotPath = Path.Combine(_folderPath, "slot.sav");

        SaveGameStorageResult result;
        DateTime lastWriteTimeUtc;
        using (new FileStream(slotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = _storage.TryGetLastWriteTimeUtc("slot", out lastWriteTimeUtc);
        }

        Assert.Equal(SaveGameStorageStatus.IoError, result.Status);
        Assert.Contains("last-write time read", result.Message);
        Assert.Contains(slotPath, result.Message);
        Assert.Equal(default, lastWriteTimeUtc);
    }

    [Fact]
    public void DefaultFolder_FollowsProjectNameReadAtFirstCall_NotAtConstruction()
    {
        string localApplicationData = Path.Combine(_rootPath, "LocalAppData");
        string projectName = new ProjectSettings().ProjectName;
        int providerCalls = 0;
        var storage = new SaveGameFileStorage(
            () =>
            {
                providerCalls++;
                return localApplicationData;
            },
            () => projectName);

        Assert.Equal(0, providerCalls);
        projectName = "AlundraGame";

        Assert.True(storage.Write("slot", new byte[] { 3 }).IsSuccess);

        string expectedFolder = Path.Combine(localApplicationData, "AlundraGame", "SaveGames");
        Assert.Equal(expectedFolder, storage.ResolveFolderPath());
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(Path.Combine(expectedFolder, "slot.sav")));

        projectName = "OtherGame";
        Assert.Equal(expectedFolder, storage.ResolveFolderPath());
        Assert.Equal(1, providerCalls);
    }

    public static IEnumerable<object[]> RefusedProjectNames()
    {
        yield return new object[] { new ProjectSettings().ProjectName };
        yield return new object[] { null };
        yield return new object[] { "" };
        yield return new object[] { "C:Game" };
        yield return new object[] { "Game:stream" };
        yield return new object[] { ".." };
        yield return new object[] { "..\\Game" };
        yield return new object[] { "Game/Sub" };
        yield return new object[] { "Game\\Sub" };
        yield return new object[] { "Game." };
        yield return new object[] { "CON" };
    }

    [Theory]
    [MemberData(nameof(RefusedProjectNames))]
    public void DefaultFolder_RefusedProjectName_ThrowsInvalidOperationAtFirstCall(string projectName)
    {
        string localApplicationData = Path.Combine(_rootPath, "LocalAppData");
        var storage = new SaveGameFileStorage(() => localApplicationData, () => projectName);

        Assert.Throws<InvalidOperationException>(() => storage.Exists("slot"));
        Assert.Throws<InvalidOperationException>(() => storage.TryRead("slot", out _));
        Assert.Throws<InvalidOperationException>(() => storage.Write("slot", new byte[] { 1 }));
        Assert.Throws<InvalidOperationException>(() => storage.Delete("slot"));
        Assert.Throws<InvalidOperationException>(() => storage.EnumerateSlots(out _));
        Assert.False(Directory.Exists(localApplicationData));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("relative\\LocalAppData")]
    public void DefaultFolder_UnavailableLocalApplicationData_ThrowsInvalidOperationAtFirstCall(string localApplicationData)
    {
        var storage = new SaveGameFileStorage(() => localApplicationData, () => "AlundraGame");

        Assert.Throws<InvalidOperationException>(() => storage.ResolveFolderPath());
        Assert.Throws<InvalidOperationException>(() => storage.Write("slot", new byte[] { 1 }));
    }

    [Fact]
    public void DefaultConstructor_ReadsGameSettingsProjectNameAtFirstCall_WithoutTouchingTheDisk()
    {
        string previousProjectName = GameSettings.ProjectSettings.ProjectName;
        try
        {
            GameSettings.ProjectSettings.ProjectName = new ProjectSettings().ProjectName;
            var storage = new SaveGameFileStorage();
            GameSettings.ProjectSettings.ProjectName = "AlundraGame";

            // ResolveFolderPath only computes the path: nothing is written under the real LocalApplicationData.
            string folderPath = storage.ResolveFolderPath();

            string expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AlundraGame",
                "SaveGames");
            Assert.Equal(expected, folderPath);
        }
        finally
        {
            GameSettings.ProjectSettings.ProjectName = previousProjectName;
        }
    }

    [Fact]
    public void ExplicitFolder_EmptyPath_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SaveGameFileStorage(""));
        Assert.ThrowsAny<ArgumentException>(() => new SaveGameFileStorage("   "));
        Assert.ThrowsAny<ArgumentException>(() => new SaveGameFileStorage((string)null));
    }

    private static byte[] CreateContent(int length, int seed)
    {
        var content = new byte[length];
        for (int i = 0; i < length; i++)
        {
            content[i] = (byte)((i * 31) + seed);
        }

        return content;
    }
}

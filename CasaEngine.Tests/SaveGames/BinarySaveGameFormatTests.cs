using System;
using System.Collections.Generic;
using CasaEngine.Framework.SaveGames;
using Xunit;

namespace CasaEngine.Tests.SaveGames;

/// <summary>
/// T2.2 of the save-game service plan: hostile binary files. Every crafted file carries a correct CRC-32 (recomputed
/// by <see cref="BinarySaveFileBuilder"/>) unless the test is about the checksum, so that the bound under test is
/// what stops the read. Refusals at the envelope go through <see cref="SaveGameFormatAssert.RefusedAtOpen"/>, which
/// checks that the metadata-only read and the full load give the same result without throwing.
/// </summary>
/// <remarks>
/// "Without allocation" is measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> around the read: the
/// claimed lengths are at least 64 MiB, the assertion allows 1 MiB. A reader that allocated (or decoded) the claimed
/// length would fail the assertion, and for lengths beyond the largest .NET array (<c>int.MaxValue</c>,
/// <c>0x7FFFFFF0</c>) it would throw <see cref="OutOfMemoryException"/>, which no reader catches.
/// </remarks>
public sealed class BinarySaveGameFormatTests
{
    private const long AllocationBudget = 1024 * 1024;
    private const int SixtyFourMebibytes = 64 * 1024 * 1024;

    public static IEnumerable<object[]> HostileLengths()
    {
        yield return new object[] { -1 };
        yield return new object[] { int.MinValue };
        yield return new object[] { int.MaxValue };
        yield return new object[] { 0x7FFFFFF0 };
        yield return new object[] { SixtyFourMebibytes };
        yield return new object[] { 5 }; // One byte more than the four that follow the prefix.
    }

    [Fact]
    public void ModifiedByte_AnywhereAfterTheContainerVersion_IsCorrupted()
    {
        byte[] original = WriteSample();

        for (int i = 8; i < original.Length; i++)
        {
            byte[] modified = (byte[])original.Clone();
            modified[i] ^= 0x01;

            SaveGameFormatAssert.RefusedAtOpen(modified, SaveGameEnvelopeStatus.Corrupted);
        }
    }

    [Fact]
    public void ModifiedByte_InTheMagicOrContainerVersion_IsNeverAccepted()
    {
        byte[] original = WriteSample();

        for (int i = 0; i < 8; i++)
        {
            byte[] modified = (byte[])original.Clone();
            modified[i] ^= 0x01;

            // The magic identifies the format; the container version is compared before the CRC.
            SaveGameFormatAssert.RefusedAtOpen(modified, i < 4 ? SaveGameEnvelopeStatus.InvalidData : SaveGameEnvelopeStatus.UnsupportedContainer);
        }
    }

    [Fact]
    public void Truncated_AtEveryLength_IsCorrupted()
    {
        byte[] original = WriteSample();

        for (int length = 1; length < original.Length; length++)
        {
            string message = SaveGameFormatAssert.RefusedAtOpen(original.AsSpan(0, length).ToArray(), SaveGameEnvelopeStatus.Corrupted);

            if (length < BinarySaveGameArchive.MinimumFileLength)
            {
                Assert.Contains("truncated", message);
            }
        }
    }

    [Fact]
    public void ShorterThanMinimum_WithAValidMagic_IsCorrupted()
    {
        byte[] file = BinarySaveFileBuilder.Header().BuildWithoutCrc();

        Assert.Equal(BinarySaveGameArchive.MinimumFileLength - 4, file.Length);
        SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.Corrupted);
    }

    [Fact]
    public void MinimumFile_NoMetadataNoPayload_IsRead()
    {
        byte[] file = BinarySaveFileBuilder.Header(dataVersion: 3).Build();

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, new DelegateSaveGameData(_ => { }, latestDataVersion: 3));
        SaveGameEnvelope.TryOpen(file, out SaveGameEnvelope envelope);

        Assert.Equal(BinarySaveGameArchive.MinimumFileLength, file.Length);
        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(3, envelope.DataVersion);
        Assert.Empty(envelope.Metadata);
    }

    [Theory]
    [MemberData(nameof(HostileLengths))]
    public void MetadataKeyLength_Hostile_IsInvalidDataWithoutAllocation(int length)
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 1).Int32(length).Raw(1, 2, 3, 4).Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);
        long allocated = SaveGameFormatAssert.AllocatedBytes(() => SaveGameEnvelope.TryOpen(file, out _));

        Assert.Contains("metadata pair 0", message);
        Assert.True(allocated < AllocationBudget, $"{allocated} bytes allocated for a claimed length of {length}.");
    }

    [Theory]
    [MemberData(nameof(HostileLengths))]
    public void MetadataValueLength_Hostile_IsInvalidDataWithoutAllocation(int length)
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 1).String("k").Int32(length).Raw(1, 2, 3, 4).Build();

        SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);
        long allocated = SaveGameFormatAssert.AllocatedBytes(() => SaveGameEnvelope.TryOpen(file, out _));

        Assert.True(allocated < AllocationBudget, $"{allocated} bytes allocated for a claimed length of {length}.");
    }

    [Theory]
    [MemberData(nameof(HostileLengths))]
    public void PayloadStringLength_Hostile_IsInvalidDataNamingTheFieldWithoutAllocation(int length)
    {
        byte[] file = BinarySaveFileBuilder.Header().Int32(length).Raw(0x41, 0x42, 0x43, 0x44).Build();
        string text = "unchanged";
        var target = new DelegateSaveGameData(archive => archive.Value("text", ref text));

        string message = SaveGameFormatAssert.RefusedAtData(file, target);
        long allocated = SaveGameFormatAssert.AllocatedBytes(() => SaveGameFormatAssert.Load(file, target));

        Assert.Contains("'text'", message);
        Assert.Equal("unchanged", text);
        Assert.True(allocated < AllocationBudget, $"{allocated} bytes allocated for a claimed length of {length}.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0x7FFFFFF0)]
    [InlineData(SixtyFourMebibytes)]
    [InlineData(2)] // The ten remaining bytes hold at most one minimal pair (8 bytes).
    public void MetadataCount_Hostile_IsInvalidDataWithoutAllocation(int count)
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: count).String("k").String("v").Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);
        long allocated = SaveGameFormatAssert.AllocatedBytes(() => SaveGameEnvelope.TryOpen(file, out _));

        Assert.Contains("metadata count", message);
        Assert.True(allocated < AllocationBudget, $"{allocated} bytes allocated for a claimed count of {count}.");
    }

    [Fact]
    public void MetadataCount_ThatTheBytesCanHold_IsRead()
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 1).String("k").String("v").Build();

        SaveGameEnvelopeResult result = SaveGameEnvelope.TryOpen(file, out SaveGameEnvelope envelope);

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal("v", envelope.Metadata["k"]);
    }

    [Fact]
    public void MetadataCount_LargerThanThePairsPresent_IsInvalidData()
    {
        // Two pairs claimed, room for two minimal pairs, but only one real pair: the reader runs out of bytes.
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 2).String("k").String("v").Raw(0, 0, 0, 0, 0, 0).Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("metadata pair 1", message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(0x7FFFFFF0)]
    [InlineData(3)] // Eight bytes follow: room for two int32 elements, not three.
    [InlineData(1)] // Fits, but the caller's array has two elements.
    public void PayloadArrayCount_Hostile_IsInvalidDataNamingTheField(int count)
    {
        byte[] file = BinarySaveFileBuilder.Header().Int32(count).Int32(7).Int32(8).Build();
        int[] values = { 1, 2 };
        var target = new DelegateSaveGameData(archive => archive.Value("ints", values));

        string message = SaveGameFormatAssert.RefusedAtData(file, target);
        long allocated = SaveGameFormatAssert.AllocatedBytes(() => SaveGameFormatAssert.Load(file, target));

        Assert.Contains("'ints'", message);
        Assert.Equal(new[] { 1, 2 }, values);
        Assert.True(allocated < AllocationBudget, $"{allocated} bytes allocated for a claimed count of {count}.");
    }

    [Fact]
    public void DuplicateMetadataKey_IsInvalidData()
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 2).String("k").String("a").String("k").String("b").Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("twice", message);
    }

    public static IEnumerable<object[]> InvalidUtf8()
    {
        yield return new object[] { new byte[] { 0xFF } };
        yield return new object[] { new byte[] { 0xC0, 0x80 } }; // Overlong NUL.
        yield return new object[] { new byte[] { 0xED, 0xA0, 0x80 } }; // Encoded surrogate U+D800.
        yield return new object[] { new byte[] { 0x41, 0xE2, 0x82 } }; // Truncated sequence.
        yield return new object[] { new byte[] { 0x80 } }; // Lone continuation byte.
    }

    [Theory]
    [MemberData(nameof(InvalidUtf8))]
    public void InvalidUtf8_InAMetadataKey_IsInvalidData(byte[] bytes)
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 1).Int32(bytes.Length).Raw(bytes).String("v").Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("UTF-8", message);
    }

    [Theory]
    [MemberData(nameof(InvalidUtf8))]
    public void InvalidUtf8_InAMetadataValue_IsInvalidData(byte[] bytes)
    {
        byte[] file = BinarySaveFileBuilder.Header(metadataCount: 1).String("k").Int32(bytes.Length).Raw(bytes).Build();

        SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);
    }

    [Theory]
    [MemberData(nameof(InvalidUtf8))]
    public void InvalidUtf8_InAPayloadString_IsInvalidDataNamingTheField(byte[] bytes)
    {
        byte[] file = BinarySaveFileBuilder.Header().Int32(bytes.Length).Raw(bytes).Build();
        string text = string.Empty;

        string message = SaveGameFormatAssert.RefusedAtData(file, new DelegateSaveGameData(archive => archive.Value("text", ref text)));

        Assert.Contains("'text'", message);
        Assert.Contains("UTF-8", message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeDataVersion_IsInvalidData(int dataVersion)
    {
        byte[] file = BinarySaveFileBuilder.Header(dataVersion: dataVersion).Build();

        string message = SaveGameFormatAssert.RefusedAtOpen(file, SaveGameEnvelopeStatus.InvalidData);

        Assert.Contains("negative", message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void ContainerVersion_OtherThanOne_IsUnsupportedContainer(int containerVersion)
    {
        byte[] withCrc = BinarySaveFileBuilder.Header(containerVersion: containerVersion).Build();
        byte[] withAnyTail = BinarySaveFileBuilder.Header(containerVersion: containerVersion).Raw(9, 9, 9, 9, 9, 9).BuildWithoutCrc();

        SaveGameFormatAssert.RefusedAtOpen(withCrc, SaveGameEnvelopeStatus.UnsupportedContainer);
        SaveGameFormatAssert.RefusedAtOpen(withAnyTail, SaveGameEnvelopeStatus.UnsupportedContainer);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void BooleanByte_OtherThanZeroOrOne_IsInvalidDataNamingTheField(byte stored)
    {
        byte[] file = BinarySaveFileBuilder.Header().Raw(stored).Build();
        bool flag = false;

        string message = SaveGameFormatAssert.RefusedAtData(file, new DelegateSaveGameData(archive => archive.Value("flag", ref flag)));

        Assert.Contains("'flag'", message);
    }

    [Fact]
    public void PayloadEndingBeforeAField_IsInvalidDataNamingTheNestedField()
    {
        byte[] file = BinarySaveFileBuilder.Header().Raw(1, 0).Build();
        int hp = 0;
        var target = new DelegateSaveGameData(archive =>
        {
            archive.BeginObject("player");
            archive.Value("hp", ref hp);
            archive.EndObject();
        });

        string message = SaveGameFormatAssert.RefusedAtData(file, target);

        Assert.Contains("'player.hp'", message);
    }

    [Fact]
    public void PayloadBytesLeftAfterTheLastField_IsInvalidData()
    {
        byte[] file = BinarySaveFileBuilder.Header().Int32(42).Raw(0).Build();
        int hp = 0;

        string message = SaveGameFormatAssert.RefusedAtData(file, new DelegateSaveGameData(archive => archive.Value("hp", ref hp)));

        Assert.Contains("left after the last field", message);
    }

    [Fact]
    public void IntegerOutOfItsTypeRange_CannotBeEncoded_ButTheWidthIsRespected()
    {
        // A ushort occupies two bytes: 0xFFFF reads back as 65535, never as a sign-extended -1.
        byte[] file = BinarySaveFileBuilder.Header().Raw(0xFF, 0xFF).Build();
        ushort value = 0;

        SaveGameEnvelopeResult result = SaveGameFormatAssert.Load(file, new DelegateSaveGameData(archive => archive.Value("v", ref value)));

        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        Assert.Equal(ushort.MaxValue, value);
    }

    [Theory]
    [InlineData(0x7FC00000)] // NaN
    [InlineData(0x7F800000)] // +Infinity
    [InlineData(unchecked((int)0xFF800000))] // -Infinity
    public void NonFiniteFloat_IsInvalidDataNamingTheField(int bits)
    {
        byte[] file = BinarySaveFileBuilder.Header().Int32(bits).Build();
        float speed = 1f;

        string message = SaveGameFormatAssert.RefusedAtData(file, new DelegateSaveGameData(archive => archive.Value("speed", ref speed)));

        Assert.Contains("'speed'", message);
    }

    private static byte[] WriteSample()
    {
        var metadata = new Dictionary<string, string> { ["location"] = "Inoa", ["time"] = "12:00" };
        SaveGameEnvelopeResult result = SaveGameEnvelope.TryWrite(SaveGameEnvelopeFormat.Binary, AllValuesSaveGameData.CreateFilled(), metadata, out byte[] file);
        Assert.Equal(SaveGameEnvelopeStatus.Success, result.Status);
        return file;
    }
}

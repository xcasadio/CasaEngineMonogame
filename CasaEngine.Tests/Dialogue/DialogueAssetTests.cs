using CasaEngine.Framework.Dialogue.Assets;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class DialogueAssetTests
{
    [Fact]
    public void TryGetLineText_PresentId_ReturnsTrueAndText()
    {
        DialogueAsset asset = DialogueAsset.FromCompiledProgram(
            "Menu",
            "Start",
            Array.Empty<byte>(),
            new Dictionary<string, string> { ["line:hello"] = "Hello there." });

        bool found = asset.TryGetLineText("line:hello", out string text);

        Assert.True(found);
        Assert.Equal("Hello there.", text);
    }

    [Fact]
    public void TryGetLineText_AbsentId_ReturnsFalse()
    {
        DialogueAsset asset = DialogueAsset.FromCompiledProgram(
            "Menu",
            "Start",
            Array.Empty<byte>(),
            new Dictionary<string, string> { ["line:hello"] = "Hello there." });

        bool found = asset.TryGetLineText("line:missing", out string text);

        Assert.False(found);
        Assert.Null(text);
    }
}

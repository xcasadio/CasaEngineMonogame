using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class YarnLineTextParserTests
{
    [Fact]
    public void Parse_PlainText_ReturnsItUnchanged()
    {
        DialogueLine line = YarnLineTextParser.Parse("Hello there.");

        Assert.Equal("Hello there.", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Parse_CharacterPrefix_SetsSpeakerAndRemovesItFromText()
    {
        DialogueLine line = YarnLineTextParser.Parse("Nom: some text");

        Assert.Equal("some text", line.Text);
        Assert.Equal("Nom", line.Speaker);
    }

    [Fact]
    public void Parse_RangedMarker_HasCorrectLength()
    {
        DialogueLine line = YarnLineTextParser.Parse("Hello [b]bold[/b] world.");

        Assert.Equal("Hello bold world.", line.Text);
        DialogueMarkupAttribute attribute = Assert.Single(line.Attributes);
        Assert.Equal("b", attribute.Name);
        Assert.Equal(6, attribute.Position);
        Assert.Equal(4, attribute.Length);
    }

    [Fact]
    public void Parse_InvalidMarkup_IsDeliveredRawWithoutThrowing()
    {
        DialogueLine line = YarnLineTextParser.Parse("Hello [b foo=] world.");

        Assert.Equal("Hello [b foo=] world.", line.Text);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void ExpandSubstitutions_ReplacesIndexedMarkers()
    {
        string expanded = YarnLineTextParser.ExpandSubstitutions("Hello {0}!", new[] { "Bob" });

        Assert.Equal("Hello Bob!", expanded);
    }

    [Fact]
    public void ExpandSubstitutions_ThenParse_MatchesRunnerBehavior()
    {
        string expanded = YarnLineTextParser.ExpandSubstitutions("Hello {0}.", new[] { "Bob" });
        DialogueLine line = YarnLineTextParser.Parse(expanded);

        Assert.Equal("Hello Bob.", line.Text);
    }
}

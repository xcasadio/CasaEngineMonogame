using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CasaEngine.Demos;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The input configuration of the demos (<c>CasaEngine.Demos/Content/demos-input.buttonsMapping</c>): the words the
/// sheets show for a binding, and the agreement between the actions the demos name in their code and the file.
/// </summary>
public class DemoInputConfigurationTests
{
    [Theory]
    [InlineData(Keys.Space, "Space")]
    [InlineData(Keys.D1, "1")]
    [InlineData(Keys.NumPad3, "Numpad 3")]
    [InlineData(Keys.Back, "Backspace")]
    [InlineData(Keys.PageUp, "Page Up")]
    [InlineData(Keys.LeftShift, "Left Shift")]
    [InlineData(Keys.Escape, "Esc")]
    [InlineData(Keys.None, "")]
    public void AKey_HasTheLabelThePlayerReads(Keys key, string expected)
    {
        Assert.Equal(expected, DemoBindingText.KeyLabel(key));
    }

    [Theory]
    [InlineData(Buttons.A, "A")]
    [InlineData(Buttons.Back, "Select")]
    [InlineData(Buttons.Start, "Start")]
    [InlineData(Buttons.LeftShoulder, "LB")]
    [InlineData(Buttons.RightStick, "RS")]
    [InlineData(Buttons.DPadUp, "D-pad up")]
    [InlineData((Buttons)0, "")]
    public void AButton_HasTheLabelThePlayerReads(Buttons button, string expected)
    {
        Assert.Equal(expected, DemoBindingText.ButtonLabel(button));
    }

    [Fact]
    public void TheFourDirections_ReadAsArrowKeysAndDPad()
    {
        var bindings = new[]
        {
            new DemoBinding(Keys.Left, Keys.None, Buttons.DPadLeft),
            new DemoBinding(Keys.Right, Keys.None, Buttons.DPadRight),
            new DemoBinding(Keys.Up, Keys.None, Buttons.DPadUp),
            new DemoBinding(Keys.Down, Keys.None, Buttons.DPadDown),
        };

        Assert.Equal("Arrow keys", DemoBindingText.KeyboardText(bindings));
        Assert.Equal("D-pad", DemoBindingText.GamePadText(bindings));
    }

    [Fact]
    public void SeveralActions_ListTheirKeysAndOnlyTheButtonsThatExist()
    {
        var bindings = new[]
        {
            new DemoBinding(Keys.W, Keys.None, 0),
            new DemoBinding(Keys.LeftShift, Keys.RightShift, 0),
            new DemoBinding(Keys.Space, Keys.None, Buttons.A),
        };

        Assert.Equal("W, Left Shift, Right Shift, Space", DemoBindingText.KeyboardText(bindings));
        Assert.Equal("A", DemoBindingText.GamePadText(bindings));
    }

    [Fact]
    public void AControlThatNamesActions_ShowsTheirBindings()
    {
        var controls = new[]
        {
            DemoControl.ForActions("throw a ball", "Throw"),
            DemoControl.ForActions("previous page", "Previous"),
            DemoControl.ForActions("mute", "Mute"),
            new DemoControl("Mouse wheel", "zoom"),
        };

        DemoBinding Resolve(string action) => action switch
        {
            "Throw" => new DemoBinding(Keys.Space, Keys.None, Buttons.A),
            "Previous" => new DemoBinding(Keys.None, Keys.None, Buttons.LeftShoulder),
            "Mute" => new DemoBinding(Keys.M, Keys.None, 0),
            _ => throw new InvalidOperationException(action),
        };

        Assert.Equal(
            "Space / A: throw a ball\nLB: previous page\nM: mute\nMouse wheel: zoom",
            DemoSheetText.FormatControls(controls, Resolve));
    }

    [Fact]
    public void AControlThatNamesActions_NeedsAResolver()
    {
        var controls = new[] { DemoControl.ForActions("throw a ball", "Throw") };

        Assert.Throws<ArgumentNullException>(() => DemoSheetText.FormatControls(controls));
    }

    [Fact]
    public void EveryActionADemoNames_IsInTheConfiguration_AndEveryActionOfTheConfigurationIsUsed()
    {
        string demosDirectory = Path.Combine(DemoScreenXamlTests.RepositoryRoot(), "CasaEngine.Demos");
        HashSet<string> declared = ReadDeclaredActions(Path.Combine(demosDirectory, "Content", "demos-input.buttonsMapping"));

        // An action name is a string literal "Group.Name" in the demos' sources; nothing else there has that shape
        // except file names and type names, which the configuration tells apart.
        var literal = new Regex("\"([A-Z][A-Za-z0-9]*\\.[A-Z][A-Za-z0-9]*)\"");
        var call = new Regex("(?:DemoInput\\.(?:IsDown|WasPressed)\\([^,]+,|DemoControl\\.ForActions\\(\"[^\"]*\",)([^;]*)");
        var used = new HashSet<string>(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (string file in Directory.EnumerateFiles(demosDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(demosDirectory, file);
            if (relative.StartsWith("bin", StringComparison.Ordinal) || relative.StartsWith("obj", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(file);
            bool isDemoInput = Path.GetFileName(file) == "DemoInput.cs";
            foreach (Match match in isDemoInput ? literal.Matches(source) : MatchesInsideCalls(call, literal, source))
            {
                string action = match.Groups[1].Value;
                used.Add(action);
                if (!declared.Contains(action))
                {
                    unknown.Add($"{relative}: {action}");
                }
            }
        }

        Assert.True(unknown.Count == 0, "Actions named in the code but missing from the configuration:\n" + string.Join("\n", unknown));

        var unused = new List<string>(declared);
        unused.RemoveAll(used.Contains);
        Assert.True(unused.Count == 0, "Actions of the configuration that no demo reads:\n" + string.Join("\n", unused));
    }

    private static IEnumerable<Match> MatchesInsideCalls(Regex call, Regex literal, string source)
    {
        foreach (Match callMatch in call.Matches(source))
        {
            foreach (Match match in literal.Matches(callMatch.Groups[1].Value))
            {
                yield return match;
            }
        }
    }

    private static HashSet<string> ReadDeclaredActions(string path)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        JObject document = JObject.Parse(File.ReadAllText(path));
        foreach (JToken button in (JArray)document["buttons"]!)
        {
            string name = (string)button["name"]!;
            Assert.True(declared.Add(name), $"The action '{name}' is declared twice.");
        }

        return declared;
    }
}

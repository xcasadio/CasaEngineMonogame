using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Input;

/// <summary>
/// Keyboard bindings are read by key position (ADR-0096): a key names its position on a US QWERTY keyboard.
/// </summary>
[Collection(KeyboardLayoutCollection.Name)]
public sealed class KeyboardLayoutTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public KeyboardLayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        TestKeyboardLayout.UseQwerty();
    }

    [Fact]
    public void OnQwerty_EveryKeyStaysItself()
    {
        KeyboardLayout.UseLayout(KeyboardLayoutCollection.Qwerty);

        for (int index = 0; index < 256; index++)
        {
            Assert.Equal((Keys)index, KeyboardLayout.FromQwertyPosition((Keys)index));
        }
    }

    [Theory]
    [InlineData(Keys.W, Keys.Z)]
    [InlineData(Keys.A, Keys.Q)]
    [InlineData(Keys.Q, Keys.A)]
    [InlineData(Keys.Z, Keys.W)]
    [InlineData(Keys.S, Keys.S)]
    [InlineData(Keys.D, Keys.D)]
    [InlineData(Keys.M, Keys.OemComma)]
    [InlineData(Keys.OemSemicolon, Keys.M)]
    [InlineData(Keys.D1, Keys.D1)]
    public void OnAzerty_AKeyGivesTheKeyAtItsQwertyPosition(Keys qwertyKey, Keys azertyKey)
    {
        KeyboardLayout.UseLayout(KeyboardLayoutCollection.Azerty);

        Assert.Equal(azertyKey, KeyboardLayout.FromQwertyPosition(qwertyKey));
    }

    [Theory]
    [InlineData(Keys.F1)]
    [InlineData(Keys.Escape)]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Space)]
    [InlineData(Keys.Up)]
    [InlineData(Keys.NumPad8)]
    [InlineData(Keys.LeftShift)]
    [InlineData(Keys.None)]
    public void OnAzerty_KeysThatDoNotMoveAreKept(Keys key)
    {
        KeyboardLayout.UseLayout(KeyboardLayoutCollection.Azerty);

        Assert.Equal(key, KeyboardLayout.FromQwertyPosition(key));
    }

    [Fact]
    public void ThisMachine_LayoutIsReadWithoutError()
    {
        KeyboardLayout.Reset();

        Keys forward = KeyboardLayout.FromQwertyPosition(Keys.W);

        // Not asserted: it depends on the layout of the machine running the tests. Written out to check by eye.
        _output.WriteLine($"W position -> {forward}, A position -> {KeyboardLayout.FromQwertyPosition(Keys.A)}");
        Assert.NotEqual(Keys.None, forward);
    }
}

/// <summary>The layout table is static: the tests that set it do not run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KeyboardLayoutCollection
{
    public const string Name = "Keyboard layout";

    public static Keys? Qwerty(int scanCode) => null;

    // French AZERTY (KBDFR), on the scan codes whose key differs from US QWERTY.
    public static Keys? Azerty(int scanCode) => scanCode switch
    {
        0x10 => Keys.A,
        0x11 => Keys.Z,
        0x1E => Keys.Q,
        0x27 => Keys.M,
        0x2C => Keys.W,
        0x32 => Keys.OemComma,
        0x33 => Keys.OemPeriod,
        0x34 => Keys.OemQuestion,
        0x35 => Keys.Oem8,
        _ => null,
    };
}

using System.Reflection;
using CasaEngine.Editor.ContentBrowser.Controls;
using CasaEngine.Editor.ContentBrowser.Models;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.ContentBrowser;

public class InlineRenameOverlayTests
{
    [Fact]
    public void Show_SelectsFileNameWithoutExtension()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        InlineRenameOverlay overlay = new(harness.Window);

        overlay.Show(new ContentItem(@"D:\TestAssets\hero.png", false), new Rectangle(10, 10, 120, 20), static (_, _) => true);

        MGTextBox textBox = GetRequiredPrivateField<MGTextBox>(overlay, "_textBox");
        Assert.Equal("hero.png", textBox.Text);
        Assert.NotNull(textBox.CurrentSelection);
        Assert.Equal(0, textBox.CurrentSelection!.Value.StartIndex);
        Assert.Equal("hero".Length, textBox.CurrentSelection.Value.EndIndex);
    }

    [Fact]
    public void Show_SelectsWholeNameForDirectories()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        InlineRenameOverlay overlay = new(harness.Window);

        overlay.Show(new ContentItem(@"D:\TestAssets\sprites.v2", true), new Rectangle(10, 10, 120, 20), static (_, _) => true);

        MGTextBox textBox = GetRequiredPrivateField<MGTextBox>(overlay, "_textBox");
        Assert.NotNull(textBox.CurrentSelection);
        Assert.Equal(0, textBox.CurrentSelection!.Value.StartIndex);
        Assert.Equal("sprites.v2".Length, textBox.CurrentSelection.Value.EndIndex);
    }

    [Fact]
    public void Show_SelectsWholeNameForLeadingDotFiles()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        InlineRenameOverlay overlay = new(harness.Window);

        overlay.Show(new ContentItem(@"D:\TestAssets\.gitignore", false), new Rectangle(10, 10, 120, 20), static (_, _) => true);

        MGTextBox textBox = GetRequiredPrivateField<MGTextBox>(overlay, "_textBox");
        Assert.NotNull(textBox.CurrentSelection);
        Assert.Equal(0, textBox.CurrentSelection!.Value.StartIndex);
        Assert.Equal(".gitignore".Length, textBox.CurrentSelection.Value.EndIndex);
    }

    [Fact]
    public void Cancel_RestoresFocusToRequestedElement()
    {
        ContentBrowserViewTestHarness harness = ContentBrowserViewTestHarness.Create();
        MGButton focusTarget = new(harness.Window, static _ => { });
        harness.Window.SetContent(focusTarget);

        //  The focus target must have been laid out and updated, otherwise it is not yet eligible for keyboard input.
        harness.AdvanceFrame(0);
        harness.AdvanceFrame(16);

        InlineRenameOverlay overlay = new(harness.Window);
        overlay.Show(new ContentItem(@"D:\TestAssets\hero.png", false), new Rectangle(10, 10, 120, 20), static (_, _) => true, focusAfterClose: focusTarget);

        overlay.Cancel();
        harness.AdvanceFrame(32);

        Assert.False(overlay.IsOpen);
        Assert.Same(focusTarget, harness.Desktop.FocusedKeyboardHandler);
    }

    private static T GetRequiredPrivateField<T>(object target, string fieldName)
        where T : class
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object? value = field.GetValue(target);
        Assert.NotNull(value);
        return Assert.IsType<T>(value);
    }
}

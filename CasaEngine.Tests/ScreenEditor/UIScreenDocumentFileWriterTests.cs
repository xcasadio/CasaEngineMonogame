using System.Text;
using CasaEngine.EditorServices.ScreenEditor.Session;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using Xunit;

namespace CasaEngine.Tests.ScreenEditor;

/// <summary>
/// T4.4 (D13): <see cref="UIScreenDocumentFileWriter"/> is <see cref="UIScreenEditorSession.Save"/>'s own
/// write logic, extracted so another caller (<c>CasaEngine.Editor.Controls.UIScreenPreviewPanel</c>) can
/// write a document with the same lossless guarantees (T4.1, engine ADR-0038) without a
/// <see cref="UIScreenEditorSession"/> of its own.
/// </summary>
public class UIScreenDocumentFileWriterTests
{
    [Fact]
    public void Write_DocumentSetBackToOriginalValues_WritesIdenticalBytes()
    {
        string tempDirectory = CreateTempDirectory();
        string xamlPath = Path.Combine(tempDirectory, "MainScreen.xaml");

        try
        {
            const string xaml = """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TitleText="InitialTitle">
  <TextBlock Name="Label" Text="Hello" />
</Window>
""";
            File.WriteAllText(xamlPath, xaml);

            var parser = new UIScreenXamlParser();
            var serializer = new UIScreenXamlSerializer();
            var document = parser.ParseFile(xamlPath);

            document.Root!.SetProperty("TitleText", "ChangedTitle");
            document.Root!.SetProperty("TitleText", "InitialTitle");

            UIScreenDocumentFileWriter.Write(document, xamlPath, serializer);

            Assert.Equal(new UTF8Encoding(false).GetBytes(xaml), File.ReadAllBytes(xamlPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Write_ModifiedDocumentWithBomAndComments_KeepsBomAndComments_AndUpdatesBaseline()
    {
        string tempDirectory = CreateTempDirectory();
        string xamlPath = Path.Combine(tempDirectory, "MainScreen.xaml");

        try
        {
            const string xaml = """
<?xml version="1.0" encoding="utf-8"?>
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TitleText="InitialTitle">
  <!-- A label. -->
  <TextBlock Name="Label" Text="Hello" />
</Window>
""";
            var originalBytes = WithByteOrderMark(xaml.Replace("\n", "\r\n"));
            File.WriteAllBytes(xamlPath, originalBytes);

            var parser = new UIScreenXamlParser();
            var serializer = new UIScreenXamlSerializer();
            var document = parser.ParseFile(xamlPath);

            document.Root!.SetProperty("TitleText", "UpdatedTitle");

            UIScreenDocumentFileWriter.Write(document, xamlPath, serializer);

            var writtenBytes = File.ReadAllBytes(xamlPath);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, writtenBytes[..3]);
            var writtenText = Encoding.UTF8.GetString(writtenBytes);
            Assert.Contains("UpdatedTitle", writtenText);
            Assert.Contains("<!-- A label. -->", writtenText);
            Assert.Equal(writtenBytes, document.OriginalBytes);

            // A second write without any further change replays these exact bytes.
            UIScreenDocumentFileWriter.Write(document, xamlPath, serializer);
            Assert.Equal(writtenBytes, File.ReadAllBytes(xamlPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static byte[] WithByteOrderMark(string xaml)
    {
        var body = new UTF8Encoding(false).GetBytes(xaml);
        var bytes = new byte[body.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        body.CopyTo(bytes, 3);
        return bytes;
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

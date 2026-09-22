namespace CasaEngine.Framework.Assets.Fonts;

/// <summary>
/// What the asset manager needs from a BMFont text file (<c>.fnt</c>) before the font itself can be
/// built: its family (<c>info face=</c>) and its page files (<c>page id= file=</c>), in page-id order.
/// The glyphs themselves are read by FontStashSharp.
/// </summary>
internal sealed class BitmapFontDescriptor
{
    private BitmapFontDescriptor(string face, IReadOnlyList<string> pageFiles)
    {
        Face = face;
        PageFiles = pageFiles;
    }

    public string Face { get; }

    public IReadOnlyList<string> PageFiles { get; }

    /// <param name="text">The content of the <c>.fnt</c> file.</param>
    /// <param name="fileName">The file, named in error messages.</param>
    /// <exception cref="InvalidDataException">No <c>face</c>, or no page.</exception>
    public static BitmapFontDescriptor Parse(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);

        string face = null;
        var pages = new SortedDictionary<int, string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("info ", StringComparison.Ordinal))
            {
                face = ReadValue(line, "face");
            }
            else if (line.StartsWith("page ", StringComparison.Ordinal))
            {
                var id = ReadValue(line, "id");
                var file = ReadValue(line, "file");
                if (id == null || file == null || !int.TryParse(id, out var pageId))
                {
                    throw new InvalidDataException($"Bitmap font '{fileName}': malformed page line '{line}'.");
                }

                pages[pageId] = file;
            }
        }

        if (string.IsNullOrEmpty(face))
        {
            throw new InvalidDataException($"Bitmap font '{fileName}': no 'info face=' entry.");
        }

        if (pages.Count == 0)
        {
            throw new InvalidDataException($"Bitmap font '{fileName}': no 'page' entry.");
        }

        return new BitmapFontDescriptor(face, new List<string>(pages.Values));
    }

    /// <summary>
    /// The catalog file name of <paramref name="relativeFileName"/>, a path written relative to the folder of
    /// <paramref name="fullFileName"/>, expressed relative to <paramref name="projectRoot"/> with the
    /// catalog's <c>\</c> separators (e.g. <c>UI\Textures\font3.png</c> for <c>Textures/font3.png</c> next to
    /// <c>…\UI\font3.fnt</c>).
    /// </summary>
    public static string CatalogFileNameNextTo(string projectRoot, string fullFileName, string relativeFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(fullFileName)) ?? string.Empty;
        var fullPage = Path.GetFullPath(Path.Combine(folder, relativeFileName));
        return Path.GetRelativePath(Path.GetFullPath(projectRoot), fullPage).Replace('/', '\\');
    }

    // BMFont text format: space-separated key=value pairs, string values in double quotes.
    private static string ReadValue(string line, string key)
    {
        var index = 0;
        while (index < line.Length)
        {
            var found = line.IndexOf(key + "=", index, StringComparison.Ordinal);
            if (found < 0)
            {
                return null;
            }

            index = found + key.Length + 1;
            if (found > 0 && line[found - 1] != ' ')
            {
                continue; // matched the tail of a longer key
            }

            if (index < line.Length && line[index] == '"')
            {
                var end = line.IndexOf('"', index + 1);
                return end < 0 ? null : line.Substring(index + 1, end - index - 1);
            }

            var space = line.IndexOf(' ', index);
            return space < 0 ? line.Substring(index) : line.Substring(index, space - index);
        }

        return null;
    }
}

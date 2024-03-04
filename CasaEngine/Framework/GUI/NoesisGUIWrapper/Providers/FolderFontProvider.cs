using Noesis;
using Path = System.IO.Path;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers
{
    using Path = Path;

    public class FolderFontProvider : FontProvider
    {
        private readonly string _rootPath;

        public FolderFontProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public override Stream OpenFont(Uri uri, string id)
        {
            foreach (var extension in new[] { ".ttf", ".otf", ".ttc" })
            {
                var fontPath = id + extension;

                if (File.Exists(Path.Combine(_rootPath, fontPath)))
                {
                    return File.OpenRead(fontPath);
                }
            }

            throw new FileNotFoundException("Font file not found", id);
        }

        public override void ScanFolder(Uri uri)
        {
            var directoryPath = Path.Combine(_rootPath, uri.OriginalString);
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var fontFilePaths = Directory.GetFiles(directoryPath, "*.*", searchOption: SearchOption.AllDirectories);

            foreach (var fontPath in fontFilePaths)
            {
                var fontFilename = fontPath.Replace("\\", "/");

                if (fontPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                    fontPath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
                    fontPath.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterFont(uri, Path.ChangeExtension(fontFilename, null));
                }
            }
        }
    }
}
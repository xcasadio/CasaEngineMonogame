using Noesis;
using Path = System.IO.Path;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers
{
    using Path = Path;

    public class FolderXamlProvider : XamlProvider
    {
        private readonly string _rootPath;

        public FolderXamlProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public override Stream? LoadXaml(Uri uri)
        {
            if (File.OpenRead(Path.Combine(_rootPath, uri.OriginalString)) is Stream originalStream)
            {
                return originalStream;
            }

            throw new FileNotFoundException("File not found", uri.OriginalString);
        }
    }
}
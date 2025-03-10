using CasaEngine.Framework.GUI.Neoforce.Skins;
using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Framework.GUI.Neoforce;

public class ArchiveManager : ContentManager, IArchiveManager
{
    public ArchiveManager(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public ArchiveManager(IServiceProvider serviceProvider, string rootDirectory) : base(serviceProvider, rootDirectory)
    {
    }

    public string[] GetDirectories(string path)
    {
        if (Directory.Exists(path))
        {
            var dirs = Directory.GetDirectories(path);

            for (var i = 0; i < dirs.Length; i++)
            {
                var parts = dirs[i].Split('\\');
                dirs[i] = parts[^1] + '\\';
            }

            return dirs;
        }
        return null;
    }
}
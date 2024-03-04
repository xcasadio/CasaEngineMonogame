using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Noesis;
using Path = System.IO.Path;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers
{
    using Path = Path;
    using Texture = Noesis.Texture;

    /// <summary>
    /// MonoGame Content texture loading provider for NoesisGUI.
    /// </summary>
    public class ContentTextureProvider : TextureProvider, IDisposable
    {
        private readonly Dictionary<string, WeakReference<Texture2D>> cache
            = new(StringComparer.OrdinalIgnoreCase);

        private readonly ContentManager contentManager;

        private readonly string rootPath;

        public ContentTextureProvider(
            ContentManager contentManager,
            string rootPath)
        {
            this.contentManager = contentManager;
            this.rootPath = rootPath;
        }

        public void Dispose()
        {
            foreach (var entry in cache)
            {
                if (entry.Value.TryGetTarget(out var texture))
                {
                    texture.Dispose();
                }
            }

            cache.Clear();
        }

        public override TextureInfo GetTextureInfo(Uri filename)
        {
            var texture = GetTexture(filename.OriginalString);
            return new TextureInfo(texture.Width, texture.Height);
        }

        public override Texture LoadTexture(Uri filename)
        {
            var texture2D = GetTexture(filename.OriginalString);
            return new NoesisTexture(filename.OriginalString, texture2D);
        }

        private Texture2D GetTexture(string filename)
        {
            if (filename.StartsWith(rootPath))
            {
                filename = filename.Remove(rootPath.Length);
            }

            filename = filename.TrimStart(Path.DirectorySeparatorChar,
                                          Path.AltDirectorySeparatorChar);

            if (cache.TryGetValue(filename, out var weakReference)
                && weakReference.TryGetTarget(out var cachedTexture)
                && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            var texture = contentManager.Load<Texture2D>(filename);
            cache[filename] = new WeakReference<Texture2D>(texture);
            return texture;
        }
    }
}
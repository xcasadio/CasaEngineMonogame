using Microsoft.Xna.Framework.Graphics;
using Noesis;
using Path = System.IO.Path;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers
{
    using Color = Color;
    using Path = Path;
    using Texture = Noesis.Texture;

    /// <summary>
    /// Default texture loading provider for NoesisGUI.
    /// Please note this is a very unefficient loader as simply added as a proof of work.
    /// You might want to replace it with <see cref="ContentTextureProvider" /> as the much more efficient solution.
    /// </summary>
    public class FolderTextureProvider : TextureProvider, IDisposable
    {
        private readonly Dictionary<string, WeakReference<Texture2D>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _rootPath;
        private readonly GraphicsDevice _graphicsDevice;

        public FolderTextureProvider(string rootPath, GraphicsDevice graphicsDevice)
        {
            _rootPath = rootPath;
            _graphicsDevice = graphicsDevice;
        }

        public new void Dispose()
        {
            foreach (KeyValuePair<string, WeakReference<Texture2D>> entry in _cache)
            {
                if (entry.Value.TryGetTarget(out var texture))
                {
                    texture?.Dispose();
                }
            }

            _cache.Clear();

            base.Dispose();
        }

        public override TextureInfo GetTextureInfo(Uri uri)
        {
            var texture = GetTexture(uri.OriginalString);

            return new TextureInfo(texture.Width, texture.Height);
        }

        public override Texture LoadTexture(Uri uri)
        {
            var texture2D = GetTexture(uri.OriginalString);

            return new NoesisTexture(uri.OriginalString, texture2D);
        }

        protected virtual Texture2D LoadTextureFromStream(FileStream fileStream)
        {
            var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

            if (texture.Format != SurfaceFormat.Color)
            {
                return texture;
            }

            // unfortunately, MonoGame loads textures as non-premultiplied alpha
            // need to premultiply alpha for correct rendering with NoesisGUI
            var buffer = new Color[texture.Width * texture.Height];
            texture.GetData(buffer);

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Color.FromNonPremultiplied(buffer[i].R, buffer[i].G, buffer[i].B, buffer[i].A);
            }

            texture.SetData(buffer);

            return texture;
        }

        private Texture2D GetTexture(string filename)
        {
            if (_cache.TryGetValue(filename, out var weakReference) &&
                weakReference.TryGetTarget(out var cachedTexture) &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            var fullPath = Path.Combine(_rootPath, filename);

            var fileStream = File.OpenRead(fullPath);

            var texture = LoadTextureFromStream(fileStream);

            _cache[filename] = new WeakReference<Texture2D>(texture);

            return texture;
        }
    }
}
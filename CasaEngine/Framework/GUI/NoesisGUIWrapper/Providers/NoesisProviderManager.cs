using Noesis;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers
{
    public class NoesisProviderManager : IDisposable
    {
        public NoesisProviderManager(
            XamlProvider xamlProvider,
            FontProvider fontProvider,
            TextureProvider textureProvider)
        {
            XamlProvider = xamlProvider;
            TextureProvider = textureProvider;
            FontProvider = fontProvider;
        }

        public FontProvider FontProvider { get; private set; }

        public TextureProvider TextureProvider { get; private set; }

        public XamlProvider XamlProvider { get; private set; }

        public void Dispose()
        {
            (XamlProvider as IDisposable)?.Dispose();
            (FontProvider as IDisposable)?.Dispose();
            (TextureProvider as IDisposable)?.Dispose();
            XamlProvider = null;
            FontProvider = null;
            TextureProvider = null;
        }
    }
}
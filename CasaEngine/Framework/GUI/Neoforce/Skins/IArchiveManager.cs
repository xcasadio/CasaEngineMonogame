namespace CasaEngine.Framework.GUI.Neoforce.Skins;

public interface IArchiveManager
{
    string RootDirectory { get; set; }
    string[] GetDirectories(string folder);
    void Unload();
    void Dispose();
    T Load<T>(string asset);
}
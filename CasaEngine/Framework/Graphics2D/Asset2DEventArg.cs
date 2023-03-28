namespace CasaEngine.Framework.Graphics2D;

public class Asset2DEventArg
    : EventArgs
{
    public Asset2DEventArg(string name)
    {
        AssetName = name;
    }

    public string AssetName;
}
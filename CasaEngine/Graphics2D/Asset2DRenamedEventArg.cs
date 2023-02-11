namespace CasaEngine.Graphics2D
{
    public class Asset2DRenamedEventArg
        : EventArgs
    {
        public Asset2DRenamedEventArg(string name, string newName)
        {
            AssetName = name;
            NewAssetName = newName;
        }

        public string AssetName;
        public string NewAssetName;
    }
}

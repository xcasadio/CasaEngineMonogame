namespace CasaEngine.Graphics2D
{
    public class Asset2DEventArg
        : EventArgs
    {
        public Asset2DEventArg(string name_)
        {
            AssetName = name_;
        }

        public string AssetName;
    }
}

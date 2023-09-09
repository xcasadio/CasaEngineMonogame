using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Controls;

public class AssetInfoViewModel : NotifyPropertyChangeBase
{
    public AssetInfo AssetInfo { get; }

    public string Name
    {
        get => AssetInfo.Name;
        set
        {
            if (value != AssetInfo.Name)
            {
                AssetInfo.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public AssetInfoViewModel(AssetInfo assetInfo)
    {
        AssetInfo = assetInfo;
    }
}
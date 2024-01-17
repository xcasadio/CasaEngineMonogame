using System;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls;

public class AssetInfoViewModel : NotifyPropertyChangeBase, IEquatable<AssetInfoViewModel>
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

    public bool Equals(AssetInfoViewModel? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return AssetInfo.Equals(other.AssetInfo);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((AssetInfoViewModel)obj);
    }

    public override int GetHashCode()
    {
        return AssetInfo.GetHashCode();
    }
}
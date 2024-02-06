using System;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorUI.Controls;

public class AssetInfoViewModel : NotifyPropertyChangeBase, IEquatable<AssetInfoViewModel>
{
    public Guid Id { get; }

    public string Name
    {
        get => AssetCatalog.Get(Id)?.Name ?? "Unknown asset name";
        set
        {
            if (value != AssetCatalog.Get(Id)?.Name)
            {
                AssetCatalog.Rename(Id, value);
                OnPropertyChanged();
            }
        }
    }

    public string FileName => AssetCatalog.Get(Id)?.FileName ?? "Unknown asset file name";

    public AssetInfoViewModel(Guid id)
    {
        Id = id;
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

        return Id == other.Id;
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
        return Id.GetHashCode();
    }
}
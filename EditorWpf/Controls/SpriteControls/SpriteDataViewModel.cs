using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.SpriteControls;

public class SpriteDataViewModel : NotifyPropertyChangeBase
{
    public SpriteData SpriteData { get; }

    public string Name
    {
        get => SpriteData.Name;
        set
        {
            if (value == SpriteData.Name) return;
            SpriteData.Name = value;
            OnPropertyChanged();
        }
    }

    public string SpriteSheetFileName
    {
        get => SpriteData.SpriteSheetFileName;
        set
        {
            if (value == SpriteData.SpriteSheetFileName) return;
            SpriteData.SpriteSheetFileName = value;
            OnPropertyChanged();
        }
    }

    public Rectangle PositionInTexture
    {
        get => SpriteData.PositionInTexture;
        set
        {
            if (value.Equals(SpriteData.PositionInTexture)) return;
            SpriteData.PositionInTexture = value;
            OnPropertyChanged();
        }
    }

    public Point Origin
    {
        get => SpriteData.Origin;
        set
        {
            if (value.Equals(SpriteData.Origin)) return;
            SpriteData.Origin = value;
            OnPropertyChanged();
        }
    }

    public SpriteDataViewModel(SpriteData spriteSpriteData)
    {
        SpriteData = spriteSpriteData;
    }
}
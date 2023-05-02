using System.ComponentModel;
using CasaEngine.Framework.Assets.Map2d;
using EditorWpf.Controls.PropertyGridTypeEditor;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.SpriteControls;

public class SpriteDataViewModel : NotifyPropertyChangeBase
{
    [Browsable(false)]
    public SpriteData SpriteData { get; }

    [ReadOnly(true)]
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

    [ReadOnly(true)]
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

    [Editor(typeof(RectangleTypeEditorControl), typeof(RectangleTypeEditorControl))]
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

    [Editor(typeof(PointTypeEditorControl), typeof(PointTypeEditorControl))]
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
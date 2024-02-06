using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CasaEngine.EditorUI.Controls.PropertyGridTypeEditor;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public class SpriteDataViewModel : NotifyPropertyChangeBase
{
    private readonly SpriteData _spriteData;

    [Browsable(false)]
    public SpriteData SpriteData => _spriteData;

    [ReadOnly(true)]
    public string Name
    {
        get => _spriteData.Name;
        set
        {
            if (value == _spriteData.Name) return;
            _spriteData.Name = value;
            OnPropertyChanged();
        }
    }

    [ReadOnly(true)]
    public Guid SpriteSheetAssetId
    {
        get => _spriteData.SpriteSheetAssetId;
        set
        {
            if (value == _spriteData.SpriteSheetAssetId) return;
            _spriteData.SpriteSheetAssetId = value;
            OnPropertyChanged();
        }
    }

    [Editor(typeof(RectangleTypeEditorControl), typeof(RectangleTypeEditorControl))]
    public Rectangle PositionInTexture
    {
        get => _spriteData.PositionInTexture;
        set
        {
            if (value.Equals(_spriteData.PositionInTexture)) return;
            _spriteData.PositionInTexture = value;
            OnPropertyChanged();
        }
    }

    [Editor(typeof(PointTypeEditorControl), typeof(PointTypeEditorControl))]
    public Point Origin
    {
        get => _spriteData.Origin;
        set
        {
            if (value.Equals(_spriteData.Origin)) return;
            _spriteData.Origin = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Collision2dViewModel> CollisionShapes { get; } = new();
    public ObservableCollection<SocketViewModel> Sockets { get; } = new();

    public SpriteDataViewModel(SpriteData spriteData)
    {
        _spriteData = spriteData;

        foreach (var collision2d in spriteData.CollisionShapes)
        {
            CollisionShapes.Add(new Collision2dViewModel(collision2d));
        }

        foreach (var socket in spriteData.Sockets)
        {
            Sockets.Add(new SocketViewModel(socket));
        }
    }
}
using System.Collections.ObjectModel;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;

namespace EditorWpf.Controls.SpriteControls;

public class SpritesModelView
{
    private readonly AssetContentManager _assetContentManager;
    public ObservableCollection<SpriteDataViewModel> SpriteDatas { get; } = new();

    public SpritesModelView(GameEditorSprite gameEditorSprite)
    {
        _assetContentManager = gameEditorSprite.Game.GameManager.AssetContentManager;
    }

    public void LoadSpriteSheet(string fileName)
    {
        var spriteDatas = SpriteLoader.LoadFromFile(fileName, _assetContentManager, SaveOption.Editor);

        SpriteDatas.Clear();
        foreach (var spriteData in spriteDatas)
        {
            SpriteDatas.Add(new SpriteDataViewModel(spriteData));
        }
    }
}
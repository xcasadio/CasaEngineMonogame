using System.Collections.ObjectModel;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using Path = System.IO.Path;

namespace EditorWpf.Controls.SpriteControls;

public class SpritesModelView
{
    private readonly AssetContentManager _assetContentManager;
    private CasaEngineGame _game;
    public ObservableCollection<SpriteDataViewModel> SpriteDatas { get; } = new();

    public SpritesModelView(GameEditorSprite gameEditorSprite)
    {
        _game = gameEditorSprite.Game;
        _assetContentManager = _game.GameManager.AssetContentManager;

        LoadAllSpriteData();
    }

    public void Add(AssetInfo assetInfo)
    {
        var spriteData = _game.GameManager.AssetContentManager.Load<SpriteData>(assetInfo, _game.GraphicsDevice);
        SpriteDatas.Add(new SpriteDataViewModel(spriteData));
    }

    private void LoadAllSpriteData()
    {
        foreach (var assetInfo in GameSettings.AssetInfoManager.AssetInfos.Where(x => Path.GetExtension(x.FileName) == Constants.FileNameExtensions.Sprite))
        {
            Add(assetInfo);
        }
    }
}
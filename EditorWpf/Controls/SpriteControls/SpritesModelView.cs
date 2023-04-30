using System.Collections.ObjectModel;
using System.IO;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.SpriteControls;

public class SpritesModelView
{
    private AssetContentManager _assetContentManager;
    public ObservableCollection<SpriteDataViewModel> SpriteDatas { get; } = new();

    public SpritesModelView(GameEditorSprite gameEditorSprite)
    {
        _assetContentManager = gameEditorSprite.Game.GameManager.AssetContentManager;
        var spriteDatas = SpriteLoader.LoadFromFile(Path.Combine(GameSettings.ProjectManager.ProjectPath, "Spritesheets", "sprites.json"),
            _assetContentManager);
        //var animations = Animation2dLoader.LoadFromFile(Path.Combine(GameSettings.ProjectManager.ProjectPath, "Spritesheets", "animations.json"),
        //    _assetContentManager);

        foreach (var spriteData in spriteDatas)
        {
            SpriteDatas.Add(new SpriteDataViewModel(spriteData));
        }
    }
}
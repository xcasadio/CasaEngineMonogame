using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Game;
using EditorWpf.Controls.Animation2dControls;

public class Animation2dListModelView
{
    private readonly AssetContentManager _assetContentManager;
    private CasaEngineGame? _game;

    public ObservableCollection<Animation2dDataViewModel> Animation2dDatas { get; } = new();

    public Animation2dListModelView(GameEditorAnimation2d gameEditor)
    {
        _game = gameEditor.Game;
        _assetContentManager = _game.GameManager.AssetContentManager;

        LoadAllAnimation2dData();
    }

    private void LoadAllAnimation2dData()
    {
        foreach (var assetInfo in GameSettings.AssetInfoManager.AssetInfos.Where(x => Path.GetExtension(x.FileName) == Constants.FileNameExtensions.Animation2d))
        {
            Add(assetInfo);
        }
    }

    //public void LoadAnimations2d(string fileName)
    //{
    //    var spriteSheetFileName = fileName.Replace(Path.GetExtension(fileName), Constants.FileNameExtensions.SpriteSheet);
    //
    //    var spriteDatas = SpriteLoader.LoadFromFile(spriteSheetFileName, _assetContentManager, SaveOption.Editor);
    //    var animations = Animation2dLoader.LoadFromFile(fileName, _assetContentManager);
    //
    //    Animation2dDatas.Clear();
    //    foreach (var animation2dData in animations)
    //    {
    //        Animation2dDatas.Add(new Animation2dDataViewModel(animation2dData));
    //    }
    //}

    public void Add(AssetInfo assetInfo)
    {
        var animation2dData = _game.GameManager.AssetContentManager.Load<Animation2dData>(assetInfo, _game.GraphicsDevice);
        Animation2dDatas.Add(new Animation2dDataViewModel(animation2dData));
    }
}
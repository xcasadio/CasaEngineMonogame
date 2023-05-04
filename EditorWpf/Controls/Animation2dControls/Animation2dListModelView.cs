using System.Collections.ObjectModel;
using System.IO;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Game;
using EditorWpf.Controls.Animation2dControls;

public class Animation2dListModelView
{
    private readonly AssetContentManager _assetContentManager;

    public ObservableCollection<Animation2dDataViewModel> Animation2dDatas { get; } = new();

    public Animation2dListModelView(GameEditorAnimation2d gameEditor)
    {
        _assetContentManager = gameEditor.Game.GameManager.AssetContentManager;
    }

    public void LoadAnimations2d(string fileName)
    {
        var spriteSheetFileName = fileName.Replace(Path.GetExtension(fileName), Constants.FileNameExtensions.SpriteSheet);

        var spriteDatas = SpriteLoader.LoadFromFile(Path.Combine(GameSettings.ProjectSettings.ProjectPath, spriteSheetFileName), _assetContentManager);
        var animations = Animation2dLoader.LoadFromFile(Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName), _assetContentManager);

        Animation2dDatas.Clear();
        foreach (var animation2dData in animations)
        {
            Animation2dDatas.Add(new Animation2dDataViewModel(animation2dData));
        }
    }
}
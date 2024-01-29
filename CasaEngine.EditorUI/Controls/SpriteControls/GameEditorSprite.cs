using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public class GameEditorSprite : GameEditor2d
{
    public StaticSpriteComponent StaticSpriteComponent { get; set; }

    public GameEditorSprite()
    {
        DataContextChanged += OnDataContextChanged;
    }

    protected override void InitializeGame()
    {

    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var spriteData = DataContext as SpriteDataViewModel;
        StaticSpriteComponent.TryLoadSpriteData(spriteData?.Name);
    }

    protected override void CreateEntityComponents(AActor entity)
    {
        StaticSpriteComponent = new StaticSpriteComponent();
        entity.RootComponent = StaticSpriteComponent;
    }
}
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.Animation2dControls;

public class GameEditorAnimation2d : GameEditor2d
{
    public AnimatedSpriteComponent AnimatedSpriteComponent { get; set; }

    public GameEditorAnimation2d()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext != null)
        {
            var animation2dDataViewModel = DataContext as Animation2dDataViewModel;
            var animation2d = new Animation2d(animation2dDataViewModel.Animation2dData);
            animation2d.Initialize();
            AnimatedSpriteComponent.SetCurrentAnimation(animation2d, true);
        }
    }

    protected override void CreateEntityComponents(AActor entity)
    {
        AnimatedSpriteComponent = new AnimatedSpriteComponent(entity);
        entity.RootComponent = AnimatedSpriteComponent;
    }
}
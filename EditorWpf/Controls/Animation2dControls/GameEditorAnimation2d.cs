using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.Animation2dControls;

public class GameEditorAnimation2d : GameEditor
{
    public AnimatedSpriteComponent AnimatedSpriteComponent { get; set; }

    public GameEditorAnimation2d()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var animation2dData = DataContext as Animation2dData;
        var animation2d = new Animation2d(animation2dData);
        animation2d.Initialize();
        AnimatedSpriteComponent.SetCurrentAnimation(animation2d, true);
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());
        Game.GameManager.CurrentWorld = new World();

        var entity = new Entity();
        entity.Coordinates.LocalPosition = new Vector3(300f, 300f, 0.0f);
        AnimatedSpriteComponent = new AnimatedSpriteComponent(entity);
        entity.ComponentManager.Components.Add(AnimatedSpriteComponent);
        entity.Initialize(Game);
        Game.GameManager.CurrentWorld.AddEntityImmediately(entity);
    }
}
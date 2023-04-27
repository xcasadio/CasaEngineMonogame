using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls;

public class GameEditorSprite : GameEditorWorld
{
    public StaticSpriteComponent StaticSpriteComponent { get; set; }

    public GameEditorSprite()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var spriteData = DataContext as SpriteData;
        StaticSpriteComponent.SpriteId = spriteData.Name;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());
        Game.GameManager.CurrentWorld = new World();

        var entity = new Entity();
        entity.Coordinates.LocalPosition = new Vector3(300f, 300f, 0.0f);
        StaticSpriteComponent = new StaticSpriteComponent(entity);
        entity.ComponentManager.Components.Add(StaticSpriteComponent);
        entity.Initialize(Game);
        Game.GameManager.CurrentWorld.AddEntityImmediately(entity);
    }
}
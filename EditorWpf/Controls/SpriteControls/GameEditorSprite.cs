using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.SpriteControls;

public class GameEditorSprite : GameEditor2d
{
    public StaticSpriteComponent StaticSpriteComponent { get; set; }

    public GameEditorSprite()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var spriteData = DataContext as SpriteDataViewModel;
        StaticSpriteComponent.SpriteId = spriteData.Name;
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        StaticSpriteComponent = new StaticSpriteComponent(entity);
        entity.ComponentManager.Components.Add(StaticSpriteComponent);
    }
}
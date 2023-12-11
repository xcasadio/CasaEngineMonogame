using CasaEngine.Editor.Controls.SpriteControls;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class GameEditorGui : GameEditor2d
{
    public GameEditorGui()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var spriteData = DataContext as SpriteDataViewModel;
    }

    protected override void CreateEntityComponents(Entity entity)
    {
    }
}
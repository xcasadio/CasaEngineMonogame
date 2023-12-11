using System.Windows.Input;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using Xceed.Wpf.AvalonDock;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class GuiEditorControl : EditorControlBase
{
    protected override string LayoutFileName => "guiEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerGui;

    public GuiEditorControl()
    {
        InitializeComponent();
        ComponentListControl.InitializeFromGameEditor(GameEditorGuiControl.GameEditor);

        dockingManagerGui.ActiveContentChanged += DockingManagerSprite_ActiveContentChanged;
    }

    private void DockingManagerSprite_ActiveContentChanged(object? sender, System.EventArgs e)
    {
        /*var layout = dockingManagerGui.ActiveContent as LayoutContent;
        var binding = new Binding("SelectedItem");

        if (layout.Content is SpriteListControl)
        {
            spriteDetailsControl.DataContext = SpriteListControl.SelectedItem;
            binding.Source = SpriteListControl;
        }
        else if (layout.Content is SpriteSocketsControl)
        {
            spriteDetailsControl.DataContext = SpriteSocketsControl.SelectedItem;
            binding.Source = SpriteSocketsControl;
        }
        else if (layout.Content is SpriteCollisionsControl)
        {
            spriteDetailsControl.DataContext = SpriteCollisionsControl.SelectedItem;
            binding.Source = SpriteCollisionsControl;
        }
        else
        {
            return;
        }

        spriteDetailsControl.SetBinding(DataContextProperty, binding);*/
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Components" => ComponentListControl,
            "Screen View" => GameEditorGuiControl,
            "Details" => ComponentDetailsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenScreen(string fileName)
    {
        //SpriteListControl.OpenSprite(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        //SpriteListControl.SaveCurrentSprite();
        e.Handled = true;
    }
}
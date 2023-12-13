using System.Windows.Input;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using Xceed.Wpf.AvalonDock;
using System.Windows.Forms;
using Screen = CasaEngine.Framework.GUI.Screen;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class GuiEditorControl : EditorControlBase
{
    protected override string LayoutFileName => "guiEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerGui;

    public GuiEditorControl()
    {
        InitializeComponent();
        ComponentListControl.InitializeFromGameEditor(GameEditorGuiControl.GameEditor);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Place Components" => PlaceComponentsControl,
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
        var assetInfo = GameSettings.AssetInfoManager.GetByFileName(fileName);
        var screen = GameEditorGuiControl.GameEditor.Game.GameManager.AssetContentManager.Load<Screen>(assetInfo);
        screen.Initialize(GameEditorGuiControl.GameEditor.Game);
        DataContext = new ScreenViewModel(screen);
        //ComponentListControl.DataContext = DataContext;
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        (DataContext as ScreenViewModel).Save();
        e.Handled = true;
    }
}
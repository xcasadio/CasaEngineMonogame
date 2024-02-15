using CasaEngine.Core.Log;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework;
using System.Windows.Input;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

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
            "ScreenGui View" => GameEditorGuiControl,
            "Details" => ComponentDetailsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenScreen(string fileName)
    {
        var assetInfo = AssetCatalog.GetByFileName(fileName);
        var screen = GameEditorGuiControl.GameEditor.Game.AssetContentManager.Load<ScreenGui>(assetInfo.Id);
        screen.Initialize();
        screen.InitializeWithWorld(GameEditorGuiControl.GameEditor.Game.GameManager.CurrentWorld);

        foreach (var control in screen.Controls)
        {
            control.Movable = true;
            control.Resizable = true;
            control.ResizerSize = 2;
            control.DesignMode = true;
            control.MovableArea = new Rectangle(0, 0,
                GameEditorGuiControl.GameEditor.Game.ScreenSizeWidth, GameEditorGuiControl.GameEditor.Game.ScreenSizeHeight);
            control.FocusGained += Control_FocusGained;
            control.FocusLost += Control_FocusLost;
        }

        DataContext = new ScreenViewModel(screen);
        //ControlListControl.DataContext = DataContext;
    }

    private void Control_FocusLost(object sender, TomShane.Neoforce.Controls.EventArgs e)
    {
        (DataContext as ScreenViewModel).SelectedControl = null;
    }

    private void Control_FocusGained(object sender, TomShane.Neoforce.Controls.EventArgs e)
    {
        var screenViewModel = (DataContext as ScreenViewModel);
        screenViewModel.SelectedControl = screenViewModel.FindControlViewModel(sender as TomShane.Neoforce.Controls.Control);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var screenViewModel = DataContext as ScreenViewModel;
        screenViewModel.Save();
        Logs.WriteInfo($"Gui screen {screenViewModel.ScreenGui.Name} saved ({screenViewModel.ScreenGui.FileName})");
        e.Handled = true;
    }
}
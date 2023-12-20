using System.Windows.Input;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;
using Xceed.Wpf.AvalonDock;
using System.Windows.Forms;
using Screen = CasaEngine.Framework.GUI.Screen;
using CasaEngine.Framework.GUI;

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
        //ComponentListControl.DataContext = DataContext;
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
        (DataContext as ScreenViewModel).Save();
        e.Handled = true;
    }
}
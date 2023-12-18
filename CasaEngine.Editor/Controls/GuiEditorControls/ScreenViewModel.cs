using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using TomShane.Neoforce.Controls;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class ScreenViewModel
{
    public Screen Screen { get; }

    public ObservableCollection<ControlViewModel> Controls { get; } = new();

    public ScreenViewModel(Screen screen)
    {
        Screen = screen;

        foreach (var control in Screen.Controls)
        {
            Controls.Add(new ControlViewModel(control));
        }
    }

    public void Save()
    {
        AssetSaver.SaveAsset(Screen.AssetInfo.FileName, Screen);
    }

    public void Add(Control control)
    {
        Screen.Add(control);
        Controls.Add(new ControlViewModel(control));
    }

    public void Remove(ControlViewModel controlViewModel)
    {
        Screen.Remove(controlViewModel.Control);
        Controls.Remove(controlViewModel);
    }
}
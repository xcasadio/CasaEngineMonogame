using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using TomShane.Neoforce.Controls;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class ScreenViewModel : NotifyPropertyChangeBase
{
    private ControlViewModel? _selectedControl;
    public Screen Screen { get; }

    public ControlViewModel? SelectedControl
    {
        get => _selectedControl;
        set
        {
            SetField(ref _selectedControl, value);

            /*foreach (var controlViewModel in Controls)
            {
                controlViewModel.Control.Selected(_selectedControl == controlViewModel);
            }*/
        }
    }

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

    public void RemoveSelectedControl()
    {
        if (SelectedControl != null)
        {
            Remove(SelectedControl);
        }
    }

    public ControlViewModel? FindControlViewModel(Control control)
    {
        foreach (var controlViewModel in Controls)
        {
            if (control == controlViewModel.Control)
            {
                return controlViewModel;
            }
        }

        return null;
    }
}
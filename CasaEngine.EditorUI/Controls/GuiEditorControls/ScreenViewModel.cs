using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using TomShane.Neoforce.Controls;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class ScreenViewModel : NotifyPropertyChangeBase
{
    private ControlViewModel? _selectedControl;
    public ScreenGui ScreenGui { get; }

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

    public ScreenViewModel(ScreenGui screenGui)
    {
        ScreenGui = screenGui;

        foreach (var control in ScreenGui.Controls)
        {
            Controls.Add(new ControlViewModel(control));
        }
    }

    public void Save()
    {
        AssetSaver.SaveAsset(ScreenGui.AssetInfo.FileName, ScreenGui);
    }

    public void Add(Control control)
    {
        ScreenGui.Add(control);
        Controls.Add(new ControlViewModel(control));
    }

    public void Remove(ControlViewModel controlViewModel)
    {
        ScreenGui.Remove(controlViewModel.Control);
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
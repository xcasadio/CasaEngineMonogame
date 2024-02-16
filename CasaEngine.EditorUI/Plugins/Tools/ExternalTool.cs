using System;
using System.Windows;

namespace CasaEngine.EditorUI.Plugins.Tools;

public class ExternalTool
{
    public Window Window
    {
        get;
        private set;
    }

    public ExternalTool(Window? window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }
}
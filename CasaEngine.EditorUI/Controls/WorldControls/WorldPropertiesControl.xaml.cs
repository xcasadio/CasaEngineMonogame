using System;
using System.Windows.Controls;
using System.Windows.Data;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class WorldPropertiesControl : UserControl
{
    private CasaEngineGame? _game;

    public WorldPropertiesControl()
    {
        InitializeComponent();
    }

    public void InitializeFromGameEditor(GameEditorWorld gameEditorWorld)
    {
        gameEditorWorld.GameStarted += OnGameGameStarted;
    }

    private void OnGameGameStarted(object? sender, EventArgs e)
    {
        _game = (CasaEngineGame)sender;

        var binding = new Binding
        {
            Source = Resources["ExternalComponentRegistered"],
            Converter = (IValueConverter)Resources["ScriptClassNameConverter"]
        };

        BindingOperations.SetBinding(comboBoxExternalComponent, ItemsControl.ItemsSourceProperty, binding);
    }

    private void ComboBoxExternalComponent_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            _game.GameManager.CurrentWorld.GameplayProxyClassName = comboBox.SelectedValue as string;
        }
    }
}
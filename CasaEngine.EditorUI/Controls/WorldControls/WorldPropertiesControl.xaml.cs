using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Data;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;

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
            Converter = (IValueConverter)Resources["ExternalComponentConverter"]
        };

        BindingOperations.SetBinding(comboBoxExternalComponent, ItemsControl.ItemsSourceProperty, binding);
    }

    private void ComboBoxExternalComponent_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var gameplayProxy = ElementFactory.Create<GameplayProxy>(((Type)comboBox.SelectedValue).Name);
            _game.GameManager.CurrentWorld.GameplayProxy = gameplayProxy;
        }
    }
}
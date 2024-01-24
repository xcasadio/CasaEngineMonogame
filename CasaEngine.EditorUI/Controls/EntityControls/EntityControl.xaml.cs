using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.EditorUI.Controls.Common;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.WpfControls;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityControl : UserControl
{
    private CasaEngineGame? _game;

    public EntityControl()
    {
        DataContextChanged += OnDataContextChanged;

        InitializeComponent();
    }

    public void InitializeFromGameEditor(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        _game = (CasaEngineGame)sender;
        entityComponentsControl.Initialize(_game);

        _game.FrameComputed += OnFrameComputed;
    }

    private void OnFrameComputed(object? sender, EventArgs e)
    {
        if (IsVisible && sender is CasaEngineGame { IsRunningInGameEditorMode: true })
        {
            var expression = Vector3ControlPosition.GetBindingExpression(Vector3Editor.ValueProperty);
            expression?.UpdateTarget();

            expression = RotationControl.GetBindingExpression(RotationEditor.ValueProperty);
            expression?.UpdateTarget();

            expression = Vector3ControlScale.GetBindingExpression(Vector3Editor.ValueProperty);
            expression?.UpdateTarget();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EntityViewModel oldEntity && oldEntity.Entity.RootComponent != null)
        {
            oldEntity.Entity.RootComponent.Coordinates.PositionChanged -= OnEntityPositionChanged;
            oldEntity.Entity.RootComponent.Coordinates.OrientationChanged -= OnEntityOrientationChanged;
            oldEntity.Entity.RootComponent.Coordinates.ScaleChanged -= OnEntityScaleChanged;
        }

        //Attach event because we can modify coordinate with the mouse with the game screenGui
        if (e.NewValue is EntityViewModel entity && entity.Entity.RootComponent != null)
        {
            entity.Entity.RootComponent.Coordinates.PositionChanged += OnEntityPositionChanged;
            entity.Entity.RootComponent.Coordinates.OrientationChanged += OnEntityOrientationChanged;
            entity.Entity.RootComponent.Coordinates.ScaleChanged += OnEntityScaleChanged;
        }
    }

    private void OnEntityOrientationChanged(object? sender, EventArgs e)
    {
        var entity = sender as SceneComponent;
        RotationControl.Value = entity.Coordinates.Rotation;
    }

    private void OnEntityPositionChanged(object? sender, EventArgs e)
    {
        var entity = sender as SceneComponent;
        Vector3ControlPosition.Value = entity.Coordinates.Position;
    }

    private void OnEntityScaleChanged(object? sender, EventArgs e)
    {
        var entity = sender as SceneComponent;
        Vector3ControlScale.Value = entity.Scale;
    }

    private void ButtonRenameEntity_OnClick(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new InputTextBox
        {
            Description = "Enter a new name",
            Title = "Rename"
        };
        var entity = (DataContext as EntityViewModel);
        inputTextBox.Text = entity.Name;
        inputTextBox.Predicate = ValidateEntityNewName;

        if (inputTextBox.ShowDialog() == true)
        {
            GameSettings.AssetCatalog.Rename(entity.Entity.Id, inputTextBox.Text);
        }
    }

    private bool ValidateEntityNewName(string? newName)
    {
        return GameSettings.AssetCatalog.CanRename(newName);
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Controls.Common;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.WpfControls;

namespace CasaEngine.Editor.Controls.EntityControls;

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
        entityComponentsControl.Game = _game;

        _game.GameManager.FrameComputed += OnFrameComputed;
    }

    private void OnFrameComputed(object? sender, EventArgs e)
    {
        if (IsVisible && sender is GameManager gameManager && gameManager.IsRunningInGameEditorMode)
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
        if (e.OldValue is EntityViewModel oldEntity)
        {
            oldEntity.Entity.PositionChanged -= OnEntityPositionChanged;
            oldEntity.Entity.OrientationChanged -= OnEntityOrientationChanged;
            oldEntity.Entity.ScaleChanged -= OnEntityScaleChanged;
        }

        //Attach event because we can modify coordinate with the mouse with the game screenGui
        if (e.NewValue is EntityViewModel entity)
        {
            entity.Entity.PositionChanged += OnEntityPositionChanged;
            entity.Entity.OrientationChanged += OnEntityOrientationChanged;
            entity.Entity.ScaleChanged += OnEntityScaleChanged;
        }
    }

    private void OnEntityOrientationChanged(object? sender, EventArgs e)
    {
        var entity = sender as Entity;
        RotationControl.Value = entity.Coordinates.LocalRotation;
    }

    private void OnEntityPositionChanged(object? sender, EventArgs e)
    {
        var entity = sender as Entity;
        Vector3ControlPosition.Value = entity.Coordinates.LocalPosition;
    }

    private void OnEntityScaleChanged(object? sender, EventArgs e)
    {
        var entity = sender as Entity;
        Vector3ControlScale.Value = entity.Scale;
    }

    private void ButtonRenameEntity_OnClick(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new InputTextBox();
        inputTextBox.Description = "Enter a new name";
        inputTextBox.Title = "Rename";
        var entity = (DataContext as EntityViewModel);
        inputTextBox.Text = entity.Name;
        inputTextBox.Predicate = ValidateEntityNewName;

        if (inputTextBox.ShowDialog() == true)
        {
            GameSettings.AssetInfoManager.Rename(entity.Entity.AssetInfo, inputTextBox.Text);
            //_game.GameManager.AssetContentManager.Rename(entity.Entity.AssetInfo, inputTextBox.Text);
        }
    }

    private bool ValidateEntityNewName(string? newName)
    {
        var entity = (DataContext as EntityViewModel);
        return GameSettings.AssetInfoManager.CanRename(entity.Entity.AssetInfo, newName);
        //return _game.GameManager.AssetContentManager.CanRename(entity.Entity.AssetInfo, newName);
    }
}
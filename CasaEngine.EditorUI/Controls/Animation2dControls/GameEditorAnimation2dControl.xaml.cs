using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.Animation2dControls;

public partial class GameEditorAnimation2dControl : UserControl
{
    private ViewId _viewId = ViewId.Empty;
    private Entity? _previewEntity;
    private AnimatedSpriteComponent? _animatedSpriteComponent;
    private float _scale = 1.0f;

    public GameEditorAnimation2dControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        if (EngineHost.Instance?.IsStarted == true)
        {
            OnEngineHostStarted(EngineHost.Instance, EventArgs.Empty);
        }
        else
        {
            EngineHost.InstanceStarted += OnEngineHostStarted;
        }
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        if (host == null)
        {
            return;
        }

        _viewId = host.RegisterEditorView(new EditorViewDefinition
        {
            Name = "Animation2d",
            ViewType = EditorViewType.Animation2d,
            InitialWidth = Math.Max((int)gameViewport.ActualWidth, 1),
            InitialHeight = Math.Max((int)gameViewport.ActualHeight, 1),
        });

        gameViewport.Attach(host, _viewId);

        var ctx = host.GetViewContext(_viewId);
        var world = ctx?.World;
        if (world == null)
        {
            return;
        }

        _previewEntity = new Entity { Name = "Animation2d Preview" };
        _animatedSpriteComponent = new AnimatedSpriteComponent();
        _previewEntity.RootComponent = _animatedSpriteComponent;

        float cx = Math.Max((int)gameViewport.ActualWidth, 1) / 2f;
        float cy = Math.Max((int)gameViewport.ActualHeight, 1) / 2f;
        _previewEntity.RootComponent.Coordinates.Position = new Vector3(cx, cy, 0f);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);

        _previewEntity.Initialize();
        _previewEntity.InitializeWithWorld(world);
        world.AddEntity(_previewEntity);

        // Apply DataContext if already set before host started.
        if (DataContext is Animation2dDataViewModel animVm)
        {
            LoadAnimation(animVm);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is Animation2dDataViewModel animVm && _animatedSpriteComponent != null)
        {
            LoadAnimation(animVm);
        }
    }

    private void LoadAnimation(Animation2dDataViewModel animVm)
    {
        if (_animatedSpriteComponent == null)
        {
            return;
        }

        var animation2d = new Animation2d(animVm.Animation2dData);
        animation2d.Initialize();
        _animatedSpriteComponent.SetCurrentAnimation(animation2d, true);
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || _previewEntity?.RootComponent == null)
        {
            return;
        }

        var value = ((e.AddedItems[0] as ComboBoxItem)?.Content as string)?.Remove(0, 1);
        if (value == null)
        {
            return;
        }

        _scale = float.Parse(value);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);
    }

    private void ButtonPlay_OnClick(object sender, RoutedEventArgs e)
    {
        // TODO: play / pause animation playback
    }

    private void ButtonNextFrame_OnClick(object sender, RoutedEventArgs e)
    {
        // TODO: step to next frame
    }
}
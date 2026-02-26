using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public partial class GameEditorSpriteControl : UserControl
{
    private ViewId _viewId = ViewId.Empty;
    private Entity? _previewEntity;
    private StaticSpriteComponent? _staticSpriteComponent;
    private float _scale = 1.0f;

    private SpriteRendererComponent? SpriteRendererComponent
        => EngineHost.Instance?.Game?.GetGameComponent<SpriteRendererComponent>();

    public GameEditorSpriteControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        if (EngineHost.Instance?.IsStarted == true)
            OnEngineHostStarted(EngineHost.Instance, EventArgs.Empty);
        else
            EngineHost.InstanceStarted += OnEngineHostStarted;
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        if (host == null) return;

        _viewId = host.RegisterEditorView(new EditorViewDefinition
        {
            Name = "Sprite",
            ViewType = EditorViewType.Sprite,
            InitialWidth = Math.Max((int)gameViewport.ActualWidth, 1),
            InitialHeight = Math.Max((int)gameViewport.ActualHeight, 1),
        });

        gameViewport.Attach(host, _viewId);

        var ctx = host.GetViewContext(_viewId);
        var world = ctx?.World;
        if (world == null) return;

        // Create the preview entity (mirrors what GameEditor2d.LoadContent used to do).
        _previewEntity = new Entity { Name = "Sprite Preview" };
        _staticSpriteComponent = new StaticSpriteComponent();
        _previewEntity.RootComponent = _staticSpriteComponent;

        float cx = _viewId.IsEmpty ? 256f : Math.Max((int)gameViewport.ActualWidth, 1) / 2f;
        float cy = _viewId.IsEmpty ? 256f : Math.Max((int)gameViewport.ActualHeight, 1) / 2f;
        _previewEntity.RootComponent.Coordinates.Position = new Vector3(cx, cy, 0f);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);

        _previewEntity.Initialize();
        _previewEntity.InitializeWithWorld(world);
        world.AddEntity(_previewEntity);

        // Apply DataContext if it was already set before the host started.
        if (DataContext is SpriteDataViewModel spriteVm)
            _staticSpriteComponent.TryLoadSpriteData(spriteVm.Name);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var spriteVm = DataContext as SpriteDataViewModel;
        _staticSpriteComponent?.TryLoadSpriteData(spriteVm?.Name);
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        var value = ((e.AddedItems[0] as ComboBoxItem)?.Content as string)?.Remove(0, 1);
        if (value == null || _previewEntity?.RootComponent == null) return;

        _scale = float.Parse(value);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);
    }

    private void ButtonHotSpot_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        if (SpriteRendererComponent != null)
            SpriteRendererComponent.IsDrawSpriteOriginEnabled = checkBox?.IsChecked ?? false;
    }

    private void ButtonSpriteBorder_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        if (SpriteRendererComponent != null)
            SpriteRendererComponent.IsDrawSpriteBorderEnabled = checkBox?.IsChecked ?? false;
    }

    private void ButtonDisplaySpriteSheet_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        if (SpriteRendererComponent != null)
            SpriteRendererComponent.IsDrawSpriteSheetEnabled = checkBox?.IsChecked ?? false;
    }

    private void Transparency_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpriteRendererComponent == null) return;
        var slider = (sender as Slider);
        SpriteRendererComponent.SpriteSheetTransparency = (int)(slider?.Value ?? 0);
    }

    private void ButtonDisplayCollisions_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        if (SpriteRendererComponent != null)
            SpriteRendererComponent.IsDrawCollisionsEnabled = checkBox?.IsChecked ?? false;
    }

    private void ButtonDisplaySockets_OnClick(object sender, RoutedEventArgs e)
    {
        // Not yet implemented.
    }
}
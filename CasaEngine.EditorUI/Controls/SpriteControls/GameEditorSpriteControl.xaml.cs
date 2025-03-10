﻿using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public partial class GameEditorSpriteControl : UserControl
{
    public GameEditorSprite GameEditor => gameEditor;
    private SpriteRendererComponent? SpriteRendererComponent => gameEditor.Game.GetGameComponent<SpriteRendererComponent>();

    public GameEditorSpriteControl()
    {
        InitializeComponent();
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (gameEditor == null)
        {
            return;
        }
        var value = ((e.AddedItems[0] as ComboBoxItem).Content as string).Remove(0, 1);
        gameEditor.Scale = float.Parse(value);
    }

    private void ButtonHotSpot_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        SpriteRendererComponent.IsDrawSpriteOriginEnabled = checkBox.IsChecked ?? false;
    }

    private void ButtonSpriteBorder_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        SpriteRendererComponent.IsDrawSpriteBorderEnabled = checkBox.IsChecked ?? false;
    }

    private void ButtonDisplaySpriteSheet_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        SpriteRendererComponent.IsDrawSpriteSheetEnabled = checkBox.IsChecked ?? false;
    }

    private void Transparency_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (gameEditor?.Game == null)
        {
            return;
        }

        var slider = (sender as Slider);
        SpriteRendererComponent.SpriteSheetTransparency = (int)slider.Value;
    }

    private void ButtonDisplayCollisions_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (sender as CheckBox);
        SpriteRendererComponent.IsDrawCollisionsEnabled = checkBox.IsChecked ?? false;
    }

    private void ButtonDisplaySockets_OnClick(object sender, RoutedEventArgs e)
    {
        //var checkBox = (sender as CheckBox);
        //Renderer2dComponent.IsDrawSpriteSheetEnabled = checkBox.IsChecked ?? false;
    }
}
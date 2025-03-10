﻿using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.DragAndDrop;

namespace CasaEngine.EditorUI.Controls.WorldControls;

public partial class PlaceEntitiesControl : UserControl
{
    public PlaceEntitiesControl()
    {
        InitializeComponent();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var frameworkElement = sender as FrameworkElement;

            DragDrop.DoDragDrop(frameworkElement,
                JsonSerializer.Serialize(new DragAndDropInfo
                {
                    Action = DragAndDropInfoAction.Create,
                    Type = frameworkElement.Tag.ToString()
                }), DragDropEffects.Copy);
        }
    }
}
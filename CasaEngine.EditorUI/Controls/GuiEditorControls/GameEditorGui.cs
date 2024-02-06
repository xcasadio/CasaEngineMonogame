using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GUI;
using Microsoft.Xna.Framework;
using Control = TomShane.Neoforce.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

public class GameEditorGui : GameEditor2d
{
    private ScreenGui _screenGui;
    private bool _keyDeletePressed = false;

    public GameEditorGui() : base(true)
    {
        Drop += OnDrop;
        UseGui = true;
        DataContextChanged += OnDataContextChanged;
        Cursor = Cursors.None;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ScreenViewModel screenViewModel)
        {
            _screenGui = screenViewModel.ScreenGui;
            Game.GameManager.CurrentWorld.ClearScreens();
            Game.GameManager.CurrentWorld.AddScreen(_screenGui);
        }
    }

    protected override void InitializeGame()
    {

    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (CameraComponent != null)
        {
            var screenXBy2 = (float)sizeInfo.NewSize.Width / 2f;
            var screenYBy2 = (float)sizeInfo.NewSize.Height / 2f;
            CameraComponent.Target = new Vector3(screenXBy2, screenYBy2, 0.0f);
        }
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        Game.UiManager.SetSkin();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var formats = e.Data.GetFormats();

        if (e.Data.GetDataPresent(DataFormats.StringFormat))
        {
            string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
            var dragAndDropInfo = JsonSerializer.Deserialize<DragAndDropInfo>(dataString);

            if (dragAndDropInfo.Action == DragAndDropInfoAction.Create)
            {
                e.Handled = true;

                var position = e.GetPosition(this);
                var type = ControlHelper.TypesByName[dragAndDropInfo.Type];
                var control = (Control)Activator.CreateInstance(type);

                control.Initialize(Game.UiManager);
                control.SetPosition((int)position.X, (int)position.Y);
                control.Movable = true;
                control.Resizable = true;
                control.ResizerSize = 2;
                control.DesignMode = true;
                control.MovableArea = new Rectangle(0, 0, Game.ScreenSizeWidth, Game.ScreenSizeHeight);
                control.Name = type.Name;

                (DataContext as ScreenViewModel).Add(control);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_keyDeletePressed && e.Key == Key.Delete)
        {
            var screenViewModel = DataContext as ScreenViewModel;
            screenViewModel.RemoveSelectedControl();

            _keyDeletePressed = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            _keyDeletePressed = false;
        }

        base.OnKeyUp(e);
    }
}
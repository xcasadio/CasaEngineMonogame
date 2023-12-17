using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using CasaEngine.Editor.DragAndDrop;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;
using Button = TomShane.Neoforce.Controls.Button;
using Control = TomShane.Neoforce.Controls.Control;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using Screen = CasaEngine.Framework.GUI.Screen;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class GameEditorGui : GameEditor2d
{
    private Screen _screen;
    private readonly Dictionary<string, Type> _types;
    private Camera3dIn2dAxisComponent _camera;

    public GameEditorGui() : base(true)
    {
        Drop += OnDrop;
        UseGui = true;
        DataContextChanged += OnDataContextChanged;
        Cursor = System.Windows.Input.Cursors.None;

        _types = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x.Namespace == typeof(Button).Namespace)
            .ToDictionary(x => x.FullName, x => x);
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ScreenViewModel screenViewModel)
        {
            _screen = screenViewModel.Screen;
            Game.GameManager.CurrentWorld.ClearScreens();
            Game.GameManager.CurrentWorld.AddScreen(_screen);
        }
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        var screenXBy2 = (float)sizeInfo.NewSize.Width / 2f;
        var screenYBy2 = (float)sizeInfo.NewSize.Height / 2f;
        _camera.Target = new Vector3(screenXBy2, screenYBy2, 0.0f);
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        Game.GameManager.UiManager.SetSkin();
        _camera = Game.GameManager.ActiveCamera as Camera3dIn2dAxisComponent;
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
                var type = _types[dragAndDropInfo.Type];
                var control = (Control)Activator.CreateInstance(type, Game.GameManager.UiManager);

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
}
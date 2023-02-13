using System;
using System.Windows;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EditorWpf.Controls.MonoGame;

public interface IMonoGameViewModel : IDisposable
{
    IGraphicsDeviceService GraphicsDeviceService { get; set; }

    void Initialize();
    void LoadContent();
    void UnloadContent();
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
    void OnActivated(object sender, EventArgs args);
    void OnDeactivated(object sender, EventArgs args);
    void OnExiting(object sender, EventArgs args);

    void SizeChanged(object sender, SizeChangedEventArgs args);
}
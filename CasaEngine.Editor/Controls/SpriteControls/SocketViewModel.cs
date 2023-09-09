using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.SpriteControls;

public class SocketViewModel : NotifyPropertyChangeBase
{
    private readonly Socket _socket;

    public string Name
    {
        get => _socket.Name;
        set
        {
            if (value == _socket.Name) return;
            _socket.Name = value;
            OnPropertyChanged();
        }
    }

    public Point Position
    {
        get => _socket.Position;
        set
        {
            if (value == _socket.Position) return;
            _socket.Position = value;
            OnPropertyChanged();
        }
    }

    public SocketViewModel(Socket socket)
    {
        _socket = socket;
    }
}
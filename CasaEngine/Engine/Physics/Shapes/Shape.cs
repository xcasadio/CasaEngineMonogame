using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics.Shapes
{
    public abstract class Shape
#if EDITOR
    : INotifyPropertyChanged
#endif
    {
        public ShapeType Type { get; }

        public Vector3 Location { get; set; }

        public Quaternion Orientation { get; set; }

        protected Shape(ShapeType type)
        {
            Type = type;
        }

#if EDITOR
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
#endif
    }
}

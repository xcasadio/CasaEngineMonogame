using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics.Shapes
{
    public abstract class Shape
    {
        public ShapeType Type { get; }

        public Vector3 Location { get; set; }
        public Quaternion Orientation { get; set; }
        public Vector3 Scale { get; set; }

        protected Shape(ShapeType type)
        {
            Type = type;
        }
    }
}

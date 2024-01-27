using System.Numerics;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector3 = Microsoft.Xna.Framework.Vector3;

#if EDITOR
using XNAGizmo;
#endif

namespace CasaEngine.Framework.SceneManagement.Components;

//Scene Components (class USceneComponent, a child of UActorComponent) support location-based behaviors that do not require
//a geometric representation.
//This includes spring arms, cameras, physical forces and constraints (but not physical objects), and even audio.
public abstract class SceneComponent : ActorComponent, IBoundingBoxable, IComponentDrawable
#if EDITOR
    , ITransformable
#endif
{
    private Matrix _lastWorldMatrix;
    public Coordinates Coordinates { get; }

    /** What we are currently attached to. If valid, RelativeLocation etc. are used relative to this object */
    public SceneComponent? Parent { get; set; }
    public List<SceneComponent> Children { get; } = new();

    public Matrix WorldMatrixWithScale
    {
        get
        {
            var result = Coordinates.LocalMatrixWithScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixWithScale;
            }

            if (Owner.Parent?.RootComponent != null)
            {
                result *= Owner.Parent.RootComponent.WorldMatrixWithScale;
            }

            return result;
        }
    }

    public Matrix WorldMatrixNoParentScale
    {
        get
        {
            var result = Coordinates.LocalMatrixWithScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixNoScale;
            }

            if (Owner.Parent?.RootComponent != null)
            {
                result *= Owner.Parent.RootComponent.WorldMatrixNoScale;
            }

            return result;
        }
    }

    public Matrix WorldMatrixNoScale
    {
        get
        {
            var result = Coordinates.LocalMatrixNoScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixNoScale;
            }

            if (Owner.Parent?.RootComponent != null)
            {
                result *= Owner.Parent.RootComponent.WorldMatrixNoScale;
            }

            return result;
        }
    }

    public Vector3 LocalPosition
    {
        get => Coordinates.Position;
        set => Coordinates.Position = value;
    }

    public Quaternion LocalOrientation
    {
        get => Coordinates.Rotation;
        set => Coordinates.Rotation = value;
    }

    public Vector3 LocalScale
    {
        get => Coordinates.Scale;
        set => Coordinates.Scale = value;
    }

    public Vector3 Position
    {
        get
        {
            var position = LocalPosition;

            if (Parent != null)
            {
                position += Parent.Position;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                position += Owner.Parent.RootComponent.Position;
            }

            return position;
        }
        set
        {
            var position = Vector3.Zero;

            if (Parent != null)
            {
                position += Parent.Position;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                position += Owner.Parent.RootComponent.Position;
            }

            LocalPosition = value - position;
        }
    }

    public Quaternion Orientation
    {
        get
        {
            var orientation = LocalOrientation;

            if (Parent != null)
            {
                orientation += Parent.Orientation;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                orientation += Owner.Parent.RootComponent.Orientation;
            }

            return orientation;
        }
        set
        {
            var orientation = Quaternion.Identity;

            if (Parent != null)
            {
                orientation += Parent.Orientation;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                orientation += Owner.Parent.RootComponent.Orientation;
            }

            LocalOrientation = value - orientation;
        }
    }

    public Vector3 Scale
    {
        get
        {
            var scale = LocalScale;

            if (Parent != null)
            {
                scale *= Parent.Position;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                scale *= Owner.Parent.RootComponent.Position;
            }

            return scale;
        }
        set
        {
            var scale = Vector3.One;

            if (Parent != null)
            {
                scale *= Parent.Position;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                scale *= Owner.Parent.RootComponent.Position;
            }

            LocalPosition = value / scale;
        }
    }

    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Orientation);
    public Vector3 Up => Vector3.Transform(Vector3.Up, Orientation);

    //Used for space partitioning
    public bool IsBoundingBoxDirty { get; protected set; }

    public BoundingBox BoundingBox => GetBoundingBox();

    public abstract BoundingBox GetBoundingBox();

    protected SceneComponent()
    {
        Coordinates = new();
    }

    protected SceneComponent(SceneComponent other) : base(other)
    {
        Coordinates = new(other.Coordinates);
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        _lastWorldMatrix = WorldMatrixWithScale;
    }

    public virtual void OnEnabledValueChange()
    {
        //do nothing
    }

    public virtual void OnScreenResized(int width, int height)
    {
        //do nothing
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (_lastWorldMatrix != WorldMatrixWithScale)
        {
            IsBoundingBoxDirty = true;
        }

        _lastWorldMatrix = WorldMatrixWithScale;
    }

    public virtual void Draw(float elapsedTime)
    {
        //do nothing
    }

    public void AddChildComponent(SceneComponent component)
    {
        component.Parent = this;
        Children.Add(component);
        component.Attach(Owner);
    }

    public void RemoveChildComponent(SceneComponent component)
    {
        component.Parent = null;
        Children.Remove(component);
        component.Detach();
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Coordinates.Load(element.GetProperty("coordinates"));
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);

        var coordinatesObject = new JObject();
        Coordinates.Save(coordinatesObject);
        node.Add("coordinates", coordinatesObject);
    }

#endif
}
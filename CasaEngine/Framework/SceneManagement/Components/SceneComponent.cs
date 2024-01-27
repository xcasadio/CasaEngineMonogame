using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

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

    public Matrix WorldMatrixWithScale
    {
        get
        {
            if (Owner.RootComponent != null && Owner.RootComponent != this)
            {
                return Coordinates.LocalMatrixWithScale * Owner.RootComponent.WorldMatrixWithScale;
            }

            return Coordinates.LocalMatrixWithScale;
        }
    }

    public Matrix WorldMatrixNoParentScale
    {
        get
        {
            if (Owner.RootComponent != this)
            {
                return Coordinates.LocalMatrixWithScale * Owner.RootComponent.WorldMatrixNoScale;
            }

            return Coordinates.LocalMatrixWithScale;
        }
    }

    public Matrix WorldMatrixNoScale
    {
        get
        {
            if (Owner.RootComponent != this)
            {
                return Coordinates.LocalMatrixNoScale * Owner.RootComponent.WorldMatrixNoScale;
            }

            return Coordinates.LocalMatrixNoScale;
        }
    }

    public Vector3 WorldPosition => Coordinates.Position + (Owner?.Parent?.RootComponent?.WorldPosition ?? Vector3.Zero);
    public Quaternion WorldRotation => Coordinates.Rotation + (Owner?.Parent?.RootComponent?.WorldRotation ?? Quaternion.Identity);
    public Vector3 WorldScale => Coordinates.Scale * (Owner?.Parent?.RootComponent?.WorldScale ?? Vector3.One);

    public Vector3 Position
    {
        get => Coordinates.Position;
        set => Coordinates.Position = value;
    }

    public Vector3 Scale
    {
        get => Coordinates.Scale;
        set => Coordinates.Scale = value;
    }

    public Quaternion Orientation
    {
        get => Coordinates.Rotation;
        set => Coordinates.Rotation = value;
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
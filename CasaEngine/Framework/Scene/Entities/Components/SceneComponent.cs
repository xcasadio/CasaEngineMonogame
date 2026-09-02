using CasaEngine.Core.Math;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Scene.Transform;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Scene.Entities.Components;

//Scene Components (class USceneComponent, a child of UActorComponent) support location-based behaviors that do not require
//a geometric representation.
//This includes spring arms, cameras, physical forces and constraints (but not physical objects), and even audio.
public abstract class SceneComponent : EntityComponent, IBoundingBoxable, IComponentDrawable, ITransformableObject
{
    private Matrix _lastWorldMatrix;
    private Matrix _lastWorldInvertTransposeMatrix;
    private LocalTransform _localTransform = null!;

    public LocalTransform LocalTransform
    {
        get => _localTransform;
        protected set => SetLocalTransform(value);
    }

    /** What we are currently attached to. If valid, RelativeLocation etc. are used relative to this object */
    public SceneComponent Parent { get; set; }
    public List<SceneComponent> Children { get; } = new();

    public Matrix WorldMatrixWithScale
    {
        get
        {
            var result = LocalTransform.LocalMatrixWithScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixWithScale;
            }

            if (Owner?.Parent?.RootComponent != null)
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
            var result = LocalTransform.LocalMatrixWithScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixNoScale;
            }

            if (Owner?.Parent?.RootComponent != null)
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
            var result = LocalTransform.LocalMatrixNoScale;

            if (Parent != null)
            {
                result *= Parent.WorldMatrixNoScale;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                result *= Owner.Parent.RootComponent.WorldMatrixNoScale;
            }

            return result;
        }
    }

    public Matrix WorldInvertTransposeMatrix => WorldMatrixWithScale.Invert().Transpose();

    public Vector3 LocalPosition
    {
        get => LocalTransform.Position;
        set => LocalTransform.Position = value;
    }

    public Quaternion LocalOrientation
    {
        get => LocalTransform.Orientation;
        set => LocalTransform.Orientation = value;
    }

    public Vector3 LocalScale
    {
        get => LocalTransform.Scale;
        set => LocalTransform.Scale = value;
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
                orientation *= Quaternion.Inverse(Parent.Orientation);
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                orientation *= Quaternion.Inverse(Owner.Parent.RootComponent.Orientation);
            }

            LocalOrientation = value * orientation;
        }
    }

    public Vector3 Scale
    {
        get
        {
            var scale = LocalScale;

            if (Parent != null)
            {
                scale *= Parent.Scale;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                scale *= Owner.Parent.RootComponent.Scale;
            }

            return scale;
        }
        set
        {
            var scale = Vector3.One;

            if (Parent != null)
            {
                scale *= Parent.Scale;
            }

            if (Owner?.Parent?.RootComponent != null)
            {
                scale *= Owner.Parent.RootComponent.Scale;
            }

            LocalScale = value / scale;
        }
    }

    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Orientation);
    public Vector3 Up => Vector3.Transform(Vector3.Up, Orientation);

    public bool IsBoundingBoxDirty { get; protected set; }

    public BoundingBox BoundingBox => GetBoundingBox();

    public virtual BoundingBox GetBoundingBox()
    {
        const float length = 0.5f;
        var localBounds = new BoundingBox(Vector3.One * -length, Vector3.One * length);
        return localBounds.Transform(WorldMatrixWithScale);
    }

    /// <summary>
    /// Marks this component's bounding box as needing to be recomputed by whatever tracks it (for example
    /// a world's spatial index, which only inspects the root and entity-level components of an entity,
    /// see <see cref="CasaEngine.Framework.Scene.Entities.Entity"/>). Additive API for callers that move
    /// a component's world position without going through its own <see cref="LocalTransform"/> (which
    /// already sets <see cref="IsBoundingBoxDirty"/> on itself), such as
    /// <see cref="RenderProjectionComponent"/> marking the entity root dirty when its projected position
    /// changes.
    /// </summary>
    public void MarkBoundingBoxDirty()
    {
        IsBoundingBoxDirty = true;
    }

    public void ClearBoundingBoxDirtyRecursive()
    {
        IsBoundingBoxDirty = false;

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].ClearBoundingBoxDirtyRecursive();
        }
    }

    protected SceneComponent()
    {
        LocalTransform = new();
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    protected SceneComponent(Guid id) : base(id)
    {
        LocalTransform = new();
    }

    private void SetLocalTransform(LocalTransform localTransform)
    {
        ArgumentNullException.ThrowIfNull(localTransform);

        if (ReferenceEquals(_localTransform, localTransform))
        {
            return;
        }

        if (_localTransform != null)
        {
            _localTransform.PositionChanged -= OnLocalTransformChanged;
            _localTransform.OrientationChanged -= OnLocalTransformChanged;
            _localTransform.ScaleChanged -= OnLocalTransformChanged;
        }

        _localTransform = localTransform;
        _localTransform.PositionChanged += OnLocalTransformChanged;
        _localTransform.OrientationChanged += OnLocalTransformChanged;
        _localTransform.ScaleChanged += OnLocalTransformChanged;
        IsBoundingBoxDirty = true;
    }

    private void OnLocalTransformChanged(object sender, EventArgs e)
    {
        IsBoundingBoxDirty = true;
    }

    protected SceneComponent(SceneComponent other) : base(other)
    {
        LocalTransform = new(other.LocalTransform);

        foreach (var child in other.Children)
        {
            AddChildComponent(child.Clone() as SceneComponent);
        }
    }

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        IsBoundingBoxDirty = true;

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Attach(actor);
        }
    }

    public override void Detach()
    {
        base.Detach();
        IsBoundingBoxDirty = true;

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Detach();
        }
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].InitializePrivate();
        }
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].InitializeWithWorld(world);
        }

        _lastWorldMatrix = WorldMatrixWithScale;
        _lastWorldInvertTransposeMatrix = _lastWorldMatrix.Invert().Transpose();
        IsBoundingBoxDirty = true;
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].OnEnabledValueChange();
        }
    }

    public virtual void OnScreenResized(int width, int height)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].OnScreenResized(width, height);
        }
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (_lastWorldMatrix != WorldMatrixWithScale)
        {
            _lastWorldMatrix = WorldMatrixWithScale;
            _lastWorldInvertTransposeMatrix = _lastWorldMatrix.Invert().Transpose();
            IsBoundingBoxDirty = true;
        }

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Update(elapsedTime);
        }
    }

    public virtual void Draw(float elapsedTime)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Draw(elapsedTime);
        }
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

    public void CopyLocalTransformFrom(LocalTransform otherLocalTransform)
    {
        LocalTransform.CopyFrom(otherLocalTransform);
        IsBoundingBoxDirty = true;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var localTransformNodeName = "local_transform";
        if (element.ContainsKey("coordinates"))
        {
            localTransformNodeName = "coordinates";
        }

        LocalTransform = element[localTransformNodeName].GetLocalTransform();

        foreach (var childComponentNode in element["children_component"])
        {
            AddChildComponent(ElementFactory.Load<SceneComponent>((JObject)childComponentNode));
        }
    }

}
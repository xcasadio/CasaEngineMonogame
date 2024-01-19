using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SpacePartitioning;

//The base class of all UE objects.
public class UObject : ISerializable
{
    private bool _isInitialized;

    public Guid Guid { get; private set; } = Guid.Empty;
    public string Name { get; set; }


    public void Initialize()
    {
        if (!_isInitialized)
        {
            InitializePrivate();
            _isInitialized = true;
        }
    }

    protected virtual void InitializePrivate()
    {
        if (Guid == Guid.Empty)
        {
            Guid = Guid.NewGuid();
        }
    }

    public virtual void Load(JsonElement element)
    {
        Guid = element.GetProperty("id").GetGuid();
        Name = element.GetProperty("name").GetString();
    }

#if EDITOR

    public virtual void Save(JObject node)
    {
        node.Add("id", Guid.ToString());
        node.Add("name", Name);
    }

#endif
}

//Actor is the base class for an Object that can be placed or spawned in a level.
public class AActor : UObject
{
    public AActor? Parent { get; private set; }
    public List<AActor> Children { get; } = new();
    public SceneComponent RootComponent { get; set; }

    public void Update(float elapsedTime)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Update(elapsedTime);
        }
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        RootComponent?.Initialize();

        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Initialize();
        }
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR
    public override void Save(JObject node)
    {
        base.Save(node);
    }
#endif
}

//Actor Components (class UActorComponent) are most useful for abstract behaviors such as movement, 
//inventory or attribute management, and other non-physical concepts.
//Actor Components do not have a transform, meaning they do not have any physical location or rotation in the world.
public abstract class ActorComponent : UObject
{
    private World.World _world;

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public void RegisterComponentWithWorld(World.World world)
    {
        _world = world;

        //if initialized

        //register event in blueprint like tick or BeginPlay
    }

    public virtual void Update(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);
    }

#endif
}

//Scene Components (class USceneComponent, a child of UActorComponent) support location-based behaviors that do not require
//a geometric representation.
//This includes spring arms, cameras, physical forces and constraints (but not physical objects), and even audio.
public abstract class SceneComponent : ActorComponent
{
    public AActor? Owner { get; }
    public Coordinates2 Coordinates { get; } = new();

    public Matrix WorldMatrix
    {
        get
        {
            if (Owner?.Parent != null)
            {
                return Coordinates.Matrix * Owner.Parent.RootComponent.WorldMatrix;
            }

            return Coordinates.Matrix;
        }
    }

    protected SceneComponent(AActor? owner = null)
    {
        Owner = owner;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public virtual void Draw(float elapsedTime)
    {

    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);
    }

#endif
}

//Primitive Components (class UPrimitiveComponent, a child of USceneComponent) are Scene Components with geometric representation, 
//which is generally used to render visual elements or to collide or overlap with physical objects.
//This includes Static or skeletal meshes, sprites or billboards, and particle systems as well as box, capsule, and sphere collision volumes. 
public abstract class PrimitiveComponent : SceneComponent
{
    //geometric representation
    //physics object

    protected PrimitiveComponent(AActor? owner = null) : base(owner)
    {

    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);
    }

    public override void Load(JsonElement element)
    {
        base.Load(element);
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);
    }

#endif
}
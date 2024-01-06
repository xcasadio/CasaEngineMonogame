using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.SceneManagement.Refactor
{
    //The base class of all UE objects.
    public class UObject
    {
        //GUID
        //Name
    }

    //Actor is the base class for an Object that can be placed or spawned in a level.
    public class AActor : UObject
    {
        public List<AActor> Children { get; } = new();
        public SceneComponent RootComponent { get; private set; }
    }

    //Actor Components (class UActorComponent) are most useful for abstract behaviors such as movement, 
    //inventory or attribute management, and other non-physical concepts.
    //Actor Components do not have a transform, meaning they do not have any physical location or rotation in the world.
    public class ActorComponent : UObject
    {

    }

    //Scene Components (class USceneComponent, a child of UActorComponent) support location-based behaviors that do not require
    //a geometric representation.
    //This includes spring arms, cameras, physical forces and constraints (but not physical objects), and even audio.
    public class SceneComponent : ActorComponent
    {
        public Coordinates Coordinates { get; } = new();
    }

    //Primitive Components (class UPrimitiveComponent, a child of USceneComponent) are Scene Components with geometric representation, 
    //which is generally used to render visual elements or to collide or overlap with physical objects.
    //This includes Static or skeletal meshes, sprites or billboards, and particle systems as well as box, capsule, and sphere collision volumes. 
    public class PrimitiveComponent : SceneComponent
    {
        //geometric representation
        //physics object
    }
}


using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

//Primitive Components (class UPrimitiveComponent, a child of USceneComponent) are Scene Components with geometric representation, 
//which is generally used to render visual elements or to collide or overlap with physical objects.
//This includes Static or skeletal meshes, sprites or billboards, and particle systems as well as box, capsule, and sphere collision volumes. 
public abstract class PrimitiveComponent : SceneComponent
{
    //geometric representation
    //physics object

    public bool CastShadows { get; set; } = true;

    public bool ReceiveShadows { get; set; } = true;

    protected PrimitiveComponent()
    {

    }

    protected PrimitiveComponent(PrimitiveComponent other) : base(other)
    {
        CastShadows = other.CastShadows;
        ReceiveShadows = other.ReceiveShadows;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        CastShadows = element["cast_shadows"]?.Value<bool>() ?? true;
        ReceiveShadows = element["receive_shadows"]?.Value<bool>() ?? true;
    }

}
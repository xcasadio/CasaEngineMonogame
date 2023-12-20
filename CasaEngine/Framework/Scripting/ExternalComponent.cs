using CasaEngine.Engine.Physics;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.Scripting;

public abstract class ExternalComponent : ISaveLoad
{
    public abstract int ExternalComponentId { get; }

    public abstract void Initialize(EntityBase entityBase);
    public abstract void Update(float elapsedTime);
    public abstract void Draw();
    public abstract void OnHit(Collision collision);
    public abstract void OnHitEnded(Collision collision);
    public abstract void OnBeginPlay(World.World world);
    public abstract void OnEndPlay(World.World world);

    public abstract void Load(JsonElement element, SaveOption option);

#if EDITOR

    public virtual void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("version", 1);
        jObject.Add("external_component_id", ExternalComponentId);
    }

#endif
}
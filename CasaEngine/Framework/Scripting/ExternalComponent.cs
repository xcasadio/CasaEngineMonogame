using CasaEngine.Core.Design;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.Scripting;

public abstract class ExternalComponent : ISaveLoad
{
    public int Type { get; }

    protected ExternalComponent(int type)
    {
        Type = type;
    }

    public abstract void Initialize(Entity entity, CasaEngineGame game);
    public abstract void Update(float elapsedTime);
    public abstract void Draw();
    public abstract void OnHit(Collision collision);
    public abstract void OnHitEnded(Collision collision);

    public abstract void Load(JsonElement element, SaveOption option);

#if EDITOR

    public virtual void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("version", 1);
        jObject.Add("type", Type);
    }

#endif
}
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Physics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scripting;

public interface IGameplayProxy
{
    void Initialize(Entity owner);
    void Initialize();
    void InitializeWithWorld(World.World world);
    void Update(float elapsedTime);
    void Draw();
    void OnHit(Collision collision);
    void OnHitEnded(Collision collision);
    void OnBeginPlay(World.World world);
    void OnEndPlay(World.World world);
    IGameplayProxy Clone();
    Guid Id { get; }
    string Name { get; set; }
    string FileName { get; set; }
    Guid AssetId { get; set; }
    void Load(JObject element);

#if EDITOR

    void Save(JObject element);

#endif
}
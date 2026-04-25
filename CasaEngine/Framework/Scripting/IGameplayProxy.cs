using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Physics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scripting;

public interface IGameplayProxy
{
    void Initialize(Entity owner);
    void Initialize();
    void InitializeWithWorld(Scene.World.World world);
    void Update(float elapsedTime);
    void Draw();
    void OnHit(Collision collision);
    void OnHitEnded(Collision collision);
    void OnBeginPlay(Scene.World.World world);
    void OnEndPlay(Scene.World.World world);
    IGameplayProxy Clone();
    Guid Id { get; }
    string Name { get; set; }
    string FileName { get; set; }
    Guid AssetId { get; set; }
    void Load(JObject element);
}
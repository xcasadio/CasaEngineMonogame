using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Framework.Entities;

public interface IExternalComponent
{
    public string Name { get; }

    public int Id { get; }

    public void Initialize();

    public void Update(float elapsedTime);

    public void Draw();

    public void OnHit(Collision collision);
    public void OnHitEnded(Collision collision);
}
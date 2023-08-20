using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Scripting;

public interface IExternalComponent
{
    public string Name { get; }

    public int Id { get; }

    public void Initialize(CasaEngineGame game);

    public void Update(float elapsedTime);

    public void Draw();

    public void OnHit(Collision collision);
    public void OnHitEnded(Collision collision);
}
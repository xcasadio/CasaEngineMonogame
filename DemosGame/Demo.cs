using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace DemosGame;

public abstract class Demo
{
    public abstract string Name { get; }
    public abstract void Initialize(CasaEngineGame game);
    public abstract void Update(GameTime gameTime);

    public void Clean(World world)
    {
        world.Clear();

        Clean();
    }

    protected abstract void Clean();
}
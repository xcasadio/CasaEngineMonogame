using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.RPGDemo.Controllers;

namespace CasaEngine.RPGDemo.Weapons;

public abstract class Weapon
{
    protected CasaEngineGame Game { get; }
    private Character _character;

    public AActor Entity { get; }

    public Character Character
    {
        get => _character;
        set
        {
            _character = value;
            Initialize();
        }
    }

    protected Weapon(CasaEngineGame game, AActor entity)
    {
        Game = game;
        Entity = entity;
    }

    protected abstract void Initialize();

    public abstract void Attach();

    public abstract void UnAttachWeapon();
}
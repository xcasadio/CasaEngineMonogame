using CasaEngine.RPGDemo.Controllers;

namespace CasaEngine.RPGDemo.Scripts;

public interface IScriptCharacter
{
    public Character Character { get; }
    public Controller Controller { get; }
}
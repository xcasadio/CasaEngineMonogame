using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using TomShane.Neoforce.Controls;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptMainHUDScreen : GameplayProxy
{
    private ScreenGui _screen;
    private ProgressBar _lifeBar;
    private Character _playerCharacter;

    public override void InitializeWithWorld(World world)
    {
        _screen = Owner as ScreenGui;
        var entity = world.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        var scriptPlayer = entity.GameplayProxy as ScriptPlayer;
        _playerCharacter = scriptPlayer.Character;
        _lifeBar = (ProgressBar)_screen.GetControlByName("ProgressBar"); // linkLifeBar
    }

    public override void Update(float elapsedTime)
    {
        _lifeBar.Value = (int)(((float)_playerCharacter.HP / (float)_playerCharacter.HPMax) * 100f);
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
    }

    public override void OnHitEnded(Collision collision)
    {
    }

    public override void OnBeginPlay(World world)
    {

    }

    public override void OnEndPlay(World world)
    {

    }

    public override void Load(JsonElement element)
    {
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject, option);
    }

#endif
}
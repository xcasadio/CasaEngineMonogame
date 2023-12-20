using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;
using TomShane.Neoforce.Controls;

namespace CasaEngine.RPGDemo.Scripts;

public class ScriptMainHUDScreen : ExternalComponent
{
    private Screen _screen;
    private ProgressBar _lifeBar;
    private Character _playerCharacter;

    public override int ExternalComponentId => (int)RpgDemoScriptIds.World;

    public override void Initialize(EntityBase entityBase)
    {
        _screen = entityBase as Screen;
        var entity = entityBase.Game.GameManager.CurrentWorld.Entities.First(x => x.Name == "character_link");
        var gamePlayComponent = entity.ComponentManager.GetComponent<GamePlayComponent>();
        var scriptPlayer = gamePlayComponent.ExternalComponent as ScriptPlayer;
        _playerCharacter = scriptPlayer.Character;
        _lifeBar = (ProgressBar)_screen.GetControlByName("ProgressBar"); // linkLifeBar

        var assetInfo = GameSettings.AssetInfoManager.GetByFileName("Screens\\MainHUD\\link_hud_portrait.sprite");
        var spriteData = entityBase.Game.GameManager.AssetContentManager.Load<SpriteData>(assetInfo);
        var sprite = Sprite.Create(spriteData, entityBase.Game.GameManager.AssetContentManager);
        var imageBox = (ImageBox)_screen.GetControlByName("ImageBox");
        imageBox.Image = sprite.Texture.Resource;
        imageBox.SourceRect = sprite.SpriteData.PositionInTexture;
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

    public override void Load(JsonElement element, SaveOption option)
    {
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}
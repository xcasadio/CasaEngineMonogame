using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.RPGDemo.Scripts.Screens;

/// <summary>
/// MGUI in-game HUD: player portrait + life bar (bottom-left).
/// <para/>
/// Its tree lives in the project asset `Screens/MainHUD/MainHUD.uiscreen`, which names its XAML. What stays
/// here is the portrait texture -- loaded from disk by the world, and optional -- and the life bar it
/// rewrites every frame. The corner it sits in is declared. The envelope is acquired through
/// <see cref="AssetContentManager"/> and held for the screen's lifetime (ADR-0038); the owner must dispose
/// this screen to give it back.
/// </summary>
public sealed class MainHUDScreen : XamlUIScreenBase
{
    /// <summary>Sub-rectangle that crops Link out of MainHUD.png.</summary>
    private static readonly Rectangle PortraitSourceRect = new(41, 0, 60, 64);

    private readonly Texture2D? _portrait;
    private readonly Func<float> _getHPPercent;   // returns 0..100
    private MGProgressBar _lifeBar;

    public override UILayer Layer   => UILayer.HUD;
    public override bool    IsModal => false;

    /// <param name="assetContentManager">Manager the screen acquires its envelope through.</param>
    /// <param name="portrait">Optional portrait texture (loaded from MainHUD.png).</param>
    /// <param name="getHPPercent">Callback returning current HP as a 0–100 percentage.</param>
    public MainHUDScreen(AssetContentManager assetContentManager, Texture2D? portrait, Func<float> getHPPercent)
        : base(assetContentManager, "MainHUD")
    {
        _portrait     = portrait;
        _getHPPercent = getHPPercent;
    }

    protected override void OnWindowLoaded(MGWindow window)
    {
        var portraitImage = FindControl<MGImage>("imgPortrait");

        if (_portrait != null)
        {
            portraitImage.Source = new MGTextureData(new CasaMonoGameImageResource(_portrait), PortraitSourceRect);
        }
        else
        {
            // The portrait has always been optional; without it the slot simply takes no room.
            portraitImage.Visibility = Visibility.Collapsed;
        }

        _lifeBar = FindControl<MGProgressBar>("pgbLife");
    }

    public override void Update(GameTime gameTime)
    {
        _lifeBar.Value = _getHPPercent();
    }
}

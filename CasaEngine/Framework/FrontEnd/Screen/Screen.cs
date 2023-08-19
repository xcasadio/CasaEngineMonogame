//-----------------------------------------------------------------------------
// Screen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.FrontEnd.Screen;

public enum ScreenState
{
    TransitionOn,
    Active,
    TransitionOff,
    Hidden,
}

public abstract class Screen
{
    private bool _isPopup;
    private TimeSpan _transitionOnTime = TimeSpan.Zero;
    private TimeSpan _transitionOffTime = TimeSpan.Zero;
    private float _transitionPosition = 1;
    private ScreenState _screenState = ScreenState.TransitionOn;
    private bool _isExiting;
    private bool _otherScreenHasFocus;
    private ScreenManagerComponent _screenManager;
    private PlayerIndex? _controllingPlayer;

    public string Name
    {
        get;
        internal set;
    }

    public bool IsPopup
    {
        get => _isPopup;
        protected set => _isPopup = value;
    }

    public TimeSpan TransitionOnTime
    {
        get => _transitionOnTime;
        set => _transitionOnTime = value;
    }

    public TimeSpan TransitionOffTime
    {
        get => _transitionOffTime;
        set => _transitionOffTime = value;
    }

    public float TransitionPosition
    {
        get => _transitionPosition;
        set => _transitionPosition = value;
    }

    public byte TransitionAlpha => (byte)(255 - TransitionPosition * 255);

    public ScreenState ScreenState
    {
        get => _screenState;
        set => _screenState = value;
    }

    public bool IsExiting
    {
        get => _isExiting;
        internal set => _isExiting = value;
    }

    public bool IsActive =>
        !_otherScreenHasFocus &&
        (_screenState == ScreenState.TransitionOn ||
         _screenState == ScreenState.Active);

    public ScreenManagerComponent ScreenManagerComponent
    {
        get => _screenManager;
        internal set => _screenManager = value;
    }

    public PlayerIndex? ControllingPlayer
    {
        get => _controllingPlayer;
        internal set => _controllingPlayer = value;
    }

    public Renderer2dComponent Renderer2dComponent
    {
        get;
        private set;
    }

    protected SpriteFont Font
    {
        get;
        private set;
    }

    protected Screen(string name)
    {
        Name = name;
    }

    protected Screen(JsonElement element, SaveOption option)
    {
        Load(element, option);
    }

    public virtual void LoadContent(Microsoft.Xna.Framework.Game game)
    {
        Renderer2dComponent = game.GetGameComponent<Renderer2dComponent>();
        Font = ((CasaEngineGame)game).GameManager.DefaultSpriteFont;
    }

    public virtual void UnloadContent() { }

    public virtual void Update(float elapsedTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        _otherScreenHasFocus = otherScreenHasFocus;

        if (_isExiting)
        {
            // If the screen is going away to die, it should transition off.
            _screenState = ScreenState.TransitionOff;

            if (!UpdateTransition(elapsedTime, _transitionOffTime, 1))
            {
                // When the transition finishes, remove the screen.
                ScreenManagerComponent.RemoveScreen(this);
            }
        }
        else if (coveredByOtherScreen)
        {
            // If the screen is covered by another, it should transition off.
            if (UpdateTransition(elapsedTime, _transitionOffTime, 1))
            {
                // Still busy transitioning.
                _screenState = ScreenState.TransitionOff;
            }
            else
            {
                // Transition finished!
                _screenState = ScreenState.Hidden;
            }
        }
        else
        {
            // Otherwise the screen should transition on and become active.
            if (UpdateTransition(elapsedTime, _transitionOnTime, -1))
            {
                // Still busy transitioning.
                _screenState = ScreenState.TransitionOn;
            }
            else
            {
                // Transition finished!
                _screenState = ScreenState.Active;
            }
        }
    }

    private bool UpdateTransition(float elapsedTime, TimeSpan time, int direction)
    {
        // How much should we move by?
        float transitionDelta;

        if (time == TimeSpan.Zero)
        {
            transitionDelta = 1;
        }
        else
        {
            transitionDelta = elapsedTime;//(float)(gameTime.ElapsedGameTime.TotalMilliseconds /
        }
        //time.TotalMilliseconds);

        // Update the transition position.
        _transitionPosition += transitionDelta * direction;

        // Did we reach the end of the transition?
        if (((direction < 0) && (_transitionPosition <= 0)) ||
            ((direction > 0) && (_transitionPosition >= 1)))
        {
            _transitionPosition = MathHelper.Clamp(_transitionPosition, 0, 1);
            return false;
        }

        // Otherwise we are still busy transitioning.
        return true;
    }

    public virtual void HandleInput(InputState input) { }

    public virtual void Draw(float elapsedTime) { }

    public virtual void Load(JsonElement element, SaveOption option)
    {
        Name = element.GetProperty("name").GetString();

        //this.TransitionAlpha = byte.Parse(el_.SelectSingleNode("TransitionAlpha").InnerText);
        TransitionOffTime = TimeSpan.Parse(element.GetProperty("transition_off_time").GetString());
        TransitionOnTime = TimeSpan.Parse(element.GetProperty("transition_on_time").GetString());
        //this.TransitionPosition = float.Parse(el_.SelectSingleNode("TransitionPosition").InnerText);
    }

    public void ExitScreen()
    {
        if (TransitionOffTime == TimeSpan.Zero)
        {
            // If the screen has a zero transition time, remove it immediately.
            ScreenManagerComponent.RemoveScreen(this);
        }
        else
        {
            // Otherwise flag that it should transition off and then exit.
            _isExiting = true;
        }
    }

    public virtual void CopyFrom(Screen screen)
    {
        _isPopup = screen._isPopup;
        _transitionOnTime = screen._transitionOnTime;
        _transitionOffTime = screen._transitionOffTime;
        _transitionPosition = screen._transitionPosition;
        _screenState = screen._screenState;
        _isExiting = screen._isExiting;
        _otherScreenHasFocus = screen._otherScreenHasFocus;
        _screenManager = screen._screenManager;
        _controllingPlayer = screen._controllingPlayer;
    }

#if EDITOR

    public bool CompareTo(object other)
    {
        if (other is Screen == false)
        {
            return false;
        }

        var screen = other as Screen;

        return _isPopup == screen._isPopup
               && _transitionOnTime == screen._transitionOnTime
               && _transitionOffTime == screen._transitionOffTime
               && _transitionPosition == screen._transitionPosition
               && _screenState == screen._screenState
               && _isExiting == screen._isExiting
               && _otherScreenHasFocus == screen._otherScreenHasFocus;
    }

    public virtual void Save(JObject jObject, SaveOption option)
    {

    }

#endif

}
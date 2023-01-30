//-----------------------------------------------------------------------------
// Screen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;
using System.Xml;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.FrontEnd.Screen
{
    public enum ScreenState
    {
        TransitionOn,
        Active,
        TransitionOff,
        Hidden,
    }

    public abstract
#if EDITOR
    partial
#endif
    class Screen : BaseObject
    {

        bool isPopup = false;
        TimeSpan transitionOnTime = TimeSpan.Zero;
        TimeSpan transitionOffTime = TimeSpan.Zero;
        float transitionPosition = 1;
        ScreenState screenState = ScreenState.TransitionOn;
        bool isExiting = false;
        bool otherScreenHasFocus;
        ScreenManagerComponent screenManager;
        PlayerIndex? controllingPlayer;



        public string Name
        {
            get;
            internal set;
        }

        public bool IsPopup
        {
            get => isPopup;
            protected set => isPopup = value;
        }

        public TimeSpan TransitionOnTime
        {
            get => transitionOnTime;
            set => transitionOnTime = value;
        }

        public TimeSpan TransitionOffTime
        {
            get => transitionOffTime;
            set => transitionOffTime = value;
        }

        public float TransitionPosition
        {
            get => transitionPosition;
            set => transitionPosition = value;
        }

        public byte TransitionAlpha => (byte)(255 - TransitionPosition * 255);

        public ScreenState ScreenState
        {
            get => screenState;
            set => screenState = value;
        }

        public bool IsExiting
        {
            get => isExiting;
            internal set => isExiting = value;
        }

        public bool IsActive =>
            !otherScreenHasFocus &&
            (screenState == ScreenState.TransitionOn ||
             screenState == ScreenState.Active);

        public ScreenManagerComponent ScreenManagerComponent
        {
            get => screenManager;
            internal set => screenManager = value;
        }

        public PlayerIndex? ControllingPlayer
        {
            get => controllingPlayer;
            internal set => controllingPlayer = value;
        }

        public Renderer2DComponent Renderer2DComponent
        {
            get;
            private set;
        }



        protected Screen(string name_)
        {
            Name = name_;
        }

        protected Screen(XmlElement el_, SaveOption opt_)
        {
            Load(el_, opt_);
        }



        public virtual void LoadContent(Renderer2DComponent r_)
        {
            Renderer2DComponent = r_;
        }

        public virtual void UnloadContent() { }



        public virtual void Update(float elapsedTime_, bool otherScreenHasFocus,
                                                      bool coveredByOtherScreen)
        {
            this.otherScreenHasFocus = otherScreenHasFocus;

            if (isExiting)
            {
                // If the screen is going away to die, it should transition off.
                screenState = ScreenState.TransitionOff;

                if (!UpdateTransition(elapsedTime_, transitionOffTime, 1))
                {
                    // When the transition finishes, remove the screen.
                    ScreenManagerComponent.RemoveScreen(this);
                }
            }
            else if (coveredByOtherScreen)
            {
                // If the screen is covered by another, it should transition off.
                if (UpdateTransition(elapsedTime_, transitionOffTime, 1))
                {
                    // Still busy transitioning.
                    screenState = ScreenState.TransitionOff;
                }
                else
                {
                    // Transition finished!
                    screenState = ScreenState.Hidden;
                }
            }
            else
            {
                // Otherwise the screen should transition on and become active.
                if (UpdateTransition(elapsedTime_, transitionOnTime, -1))
                {
                    // Still busy transitioning.
                    screenState = ScreenState.TransitionOn;
                }
                else
                {
                    // Transition finished!
                    screenState = ScreenState.Active;
                }
            }
        }

        bool UpdateTransition(float elapsedTime_, TimeSpan time, int direction)
        {
            // How much should we move by?
            float transitionDelta;

            if (time == TimeSpan.Zero)
                transitionDelta = 1;
            else
                transitionDelta = elapsedTime_;//(float)(gameTime.ElapsedGameTime.TotalMilliseconds /
                                               //time.TotalMilliseconds);

            // Update the transition position.
            transitionPosition += transitionDelta * direction;

            // Did we reach the end of the transition?
            if (((direction < 0) && (transitionPosition <= 0)) ||
                ((direction > 0) && (transitionPosition >= 1)))
            {
                transitionPosition = MathHelper.Clamp(transitionPosition, 0, 1);
                return false;
            }

            // Otherwise we are still busy transitioning.
            return true;
        }

        public virtual void HandleInput(InputState input) { }

        public virtual void Draw(float elapsedTime_) { }



        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            Name = el_.Attributes["name"].Value;

            //this.TransitionAlpha = byte.Parse(el_.SelectSingleNode("TransitionAlpha").InnerText);
            this.TransitionOffTime = TimeSpan.Parse(el_.SelectSingleNode("TransitionOffTime").InnerText);
            this.TransitionOnTime = TimeSpan.Parse(el_.SelectSingleNode("TransitionOnTime").InnerText);
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
                isExiting = true;
            }
        }

        public virtual void CopyFrom(Screen screen_)
        {
            isPopup = screen_.isPopup;
            transitionOnTime = screen_.transitionOnTime;
            transitionOffTime = screen_.transitionOffTime;
            transitionPosition = screen_.transitionPosition;
            screenState = screen_.screenState;
            isExiting = screen_.isExiting;
            otherScreenHasFocus = screen_.otherScreenHasFocus;
            screenManager = screen_.screenManager;
            controllingPlayer = screen_.controllingPlayer;
        }

#if EDITOR

        public override bool CompareTo(BaseObject other_)
        {
            if (other_ is Screen == false)
            {
                return false;
            }

            Screen screen = other_ as Screen;

            return isPopup == screen.isPopup
                && transitionOnTime == screen.transitionOnTime
                && transitionOffTime == screen.transitionOffTime
                && transitionPosition == screen.transitionPosition
                && screenState == screen.screenState
                && isExiting == screen.isExiting
                && otherScreenHasFocus == screen.otherScreenHasFocus;
        }

#endif

        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }

    }
}

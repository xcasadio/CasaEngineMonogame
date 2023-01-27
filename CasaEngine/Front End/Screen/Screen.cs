#region File Description
//-----------------------------------------------------------------------------
// Screen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;
using System.Xml;
using CasaEngine.FrontEnd.Screen.Gadget;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;
#endregion

namespace CasaEngine.FrontEnd.Screen
{
    /// <summary>
    /// Enum describes the screen transition state.
    /// </summary>
    public enum ScreenState
    {
        TransitionOn,
        Active,
        TransitionOff,
        Hidden,
    }

    /// <summary>
    /// A screen is a single layer that has update and draw logic, and which
    /// can be combined with other layers to build up a complex menu system.
    /// For instance the main menu, the options menu, the "are you sure you
    /// want to quit" message box, and the main game itself are all implemented
    /// as screens.
    /// </summary>
    public abstract 
#if EDITOR
    partial
#endif
    class Screen : BaseObject
    {
        #region Fields

        bool isPopup = false;
        TimeSpan transitionOnTime = TimeSpan.Zero;
        TimeSpan transitionOffTime = TimeSpan.Zero;
        float transitionPosition = 1;
        ScreenState screenState = ScreenState.TransitionOn;
        bool isExiting = false;
        bool otherScreenHasFocus;
        ScreenManagerComponent screenManager;
        PlayerIndex? controllingPlayer;

        #endregion

        #region Properties

        /// <summary>
		/// Gets
		/// </summary>
		public string Name
		{
			get;
			internal set;
		}

        /// <summary>
        /// Normally when one screen is brought up over the top of another,
        /// the first screen will transition off to make room for the new
        /// one. This property indicates whether the screen is only a small
        /// popup, in which case screens underneath it do not need to bother
        /// transitioning off.
        /// </summary>
        public bool IsPopup
        {
            get { return isPopup; }
            protected set { isPopup = value; }
        }

        /// <summary>
        /// Indicates how long the screen takes to
        /// transition on when it is activated.
        /// </summary>
        public TimeSpan TransitionOnTime
        {
            get { return transitionOnTime; }
            set { transitionOnTime = value; }
        }        

        /// <summary>
        /// Indicates how long the screen takes to
        /// transition off when it is deactivated.
        /// </summary>
        public TimeSpan TransitionOffTime
        {
            get { return transitionOffTime; }
            set { transitionOffTime = value; }
        }

        /// <summary>
        /// Gets the current position of the screen transition, ranging
        /// from zero (fully active, no transition) to one (transitioned
        /// fully off to nothing).
        /// </summary>
        public float TransitionPosition
        {
            get { return transitionPosition; }
            set { transitionPosition = value; }
        }       

        /// <summary>
        /// Gets the current alpha of the screen transition, ranging
        /// from 255 (fully active, no transition) to 0 (transitioned
        /// fully off to nothing).
        /// </summary>
        public byte TransitionAlpha
        {
            get { return (byte)(255 - TransitionPosition * 255); }
        }

        /// <summary>
        /// Gets the current screen transition state.
        /// </summary>
        public ScreenState ScreenState
        {
            get { return screenState; }
            set { screenState = value; }
        }

        /// <summary>
        /// There are two possible reasons why a screen might be transitioning
        /// off. It could be temporarily going away to make room for another
        /// screen that is on top of it, or it could be going away for good.
        /// This property indicates whether the screen is exiting for real:
        /// if set, the screen will automatically remove itself as soon as the
        /// transition finishes.
        /// </summary>
        public bool IsExiting
        {
            get { return isExiting; }
            internal set { isExiting = value; }
        }

        /// <summary>
        /// Checks whether this screen is active and can respond to user input.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return !otherScreenHasFocus &&
                       (screenState == ScreenState.TransitionOn ||
                        screenState == ScreenState.Active);
            }
        }

        /// <summary>
        /// Gets the manager that this screen belongs to.
        /// </summary>
		public ScreenManagerComponent ScreenManagerComponent
        {
            get { return screenManager; }
            internal set { screenManager = value; }
        }

        /// <summary>
        /// Gets the index of the player who is currently controlling this screen,
        /// or null if it is accepting input from any player. This is used to lock
        /// the game to a specific player profile. The main menu responds to input
        /// from any connected gamepad, but whichever player makes a selection from
        /// this menu is given control over all subsequent screens, so other gamepads
        /// are inactive until the controlling player returns to the main menu.
        /// </summary>
        public PlayerIndex? ControllingPlayer
        {
            get { return controllingPlayer; }
            internal set { controllingPlayer = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Renderer2DComponent Renderer2DComponent
        {
            get;
            private set;
        }

        #endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		protected Screen(string name_)
		{
			Name = name_;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        protected Screen(XmlElement el_, SaveOption opt_)
        {
            Load(el_, opt_);
        }

		#endregion // Constructors

		#region Initialization

		/// <summary>
        /// Load graphics content for the screen.
        /// </summary>
        public virtual void LoadContent(Renderer2DComponent r_)
        {
            Renderer2DComponent = r_;
        }

        /// <summary>
        /// Unload content for the screen.
        /// </summary>
        public virtual void UnloadContent() { }

        #endregion

        #region Update and Draw

		/// <summary>
		/// Allows the screen to run logic, such as updating the transition position.
        /// Unlike HandleInput, this method is called regardless of whether the screen
        /// is active, hidden, or in the middle of a transition.
		/// </summary>
		/// <param name="elapsedTime_"></param>
		/// <param name="otherScreenHasFocus"></param>
		/// <param name="coveredByOtherScreen"></param>
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

        /// <summary>
        /// Helper for updating the screen transition position.
        /// </summary>
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

        /// <summary>
        /// Allows the screen to handle user input. Unlike Update, this method
        /// is only called when the screen is active, and not when some other
        /// screen has taken the focus.
        /// </summary>
        public virtual void HandleInput(InputState input) { }

        /// <summary>
        /// This is called when the screen should draw itself.
        /// </summary>
		public virtual void Draw(float elapsedTime_) { }

        #endregion

        #region Public Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            Name = el_.Attributes["name"].Value;

            //this.TransitionAlpha = byte.Parse(el_.SelectSingleNode("TransitionAlpha").InnerText);
            this.TransitionOffTime = TimeSpan.Parse(el_.SelectSingleNode("TransitionOffTime").InnerText);
            this.TransitionOnTime = TimeSpan.Parse(el_.SelectSingleNode("TransitionOnTime").InnerText);
            //this.TransitionPosition = float.Parse(el_.SelectSingleNode("TransitionPosition").InnerText);
        }

        /// <summary>
		/// Tells the screen to go away. Unlike ScreenManagerComponent.RemoveScreen, which
        /// instantly kills the screen, this method respects the transition timings
        /// and will give the screen a chance to gradually transition off.
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="screen_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="screen_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}

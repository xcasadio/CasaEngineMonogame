//-----------------------------------------------------------------------------
// DebugSystem.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

/*
 * To get started with the GameDebugTools, go to your main game class, override the Initialize method and add the
 * following line of code:
 * 
 * GameDebugTools.DebugSystem.Initialize(this, "MyFont");
 * 
 * where "MyFont" is the name a SpriteFont in your content project. This method will initialize all of the debug
 * tools and add the necessary components to your game. To begin instrumenting your game, add the following line of 
 * code to the top of your Update method:
 *
 * GameDebugTools.DebugSystem.Instance.TimeRuler.StartFrame()
 * 
 * Once you have that in place, you can add markers throughout your game by surrounding your code with BeginMark and
 * EndMark calls of the TimeRuler. For example:
 * 
 * GameDebugTools.DebugSystem.Instance.TimeRuler.BeginMark("SomeCode", Color.Blue);
 * // Your code goes here
 * GameDebugTools.DebugSystem.Instance.TimeRuler.EndMark("SomeCode");
 * 
 * Then you can display these results by setting the Visible property of the TimeRuler to true. This will give you a
 * visual display you can use to profile your game for optimizations.
 *
 * The GameDebugTools also come with an FpsCounter and a DebugCommandUI, which allows you to type commands at runtime
 * and toggle the various displays as well as registering your own commands that enable you to alter your game without
 * having to restart.
 */

namespace CasaEngine.Framework.Debugger
{
    public sealed class DebugSystem
    {
        private static DebugSystem? _singletonInstance;

        public static DebugSystem? Instance => _singletonInstance;

        public DebugManager DebugManager { get; private set; }

        public DebugCommandUi DebugCommandUi { get; private set; }

        public FpsCounter FpsCounter { get; private set; }

        public TimeRuler TimeRuler { get; private set; }

        public static DebugSystem? Initialize(Microsoft.Xna.Framework.Game game)
        {
            // if the singleton exists, return that; we don't want two systems being created for a game
            if (_singletonInstance != null)
            {
                return _singletonInstance;
            }

            // Create the system
            _singletonInstance = new DebugSystem();

            // Create all of the system components
            _singletonInstance.DebugManager = new DebugManager(game);
            game.Components.Add(_singletonInstance.DebugManager);

            _singletonInstance.DebugCommandUi = new DebugCommandUi(game);
            game.Components.Add(_singletonInstance.DebugCommandUi);

            _singletonInstance.FpsCounter = new FpsCounter(game);
            game.Components.Add(_singletonInstance.FpsCounter);

            _singletonInstance.TimeRuler = new TimeRuler(game);
            game.Components.Add(_singletonInstance.TimeRuler);

            return _singletonInstance;
        }
    }
}

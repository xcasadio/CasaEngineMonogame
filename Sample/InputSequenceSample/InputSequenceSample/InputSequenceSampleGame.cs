using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using CasaEngine.Input;

namespace InputSequenceSample
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class InputSequenceSampleGame : 
        Microsoft.Xna.Framework.Game
    {
        public enum ButtonCode
        {
            PL,
            PM,
            PH,
            KL,
            KM,
            KH,
            Left,
            Right,
            Up,
            Down
        }

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        SpriteFont spriteFont;

        // Direction textures.
        Texture2D upTexture;
        Texture2D downTexture;
        Texture2D leftTexture;
        Texture2D rightTexture;
        Texture2D upLeftTexture;
        Texture2D upRightTexture;
        Texture2D downLeftTexture;
        Texture2D downRightTexture;

        // Button textures.
        Texture2D aButtonTexture;
        Texture2D bButtonTexture;
        Texture2D xButtonTexture;
        Texture2D yButtonTexture;

        // Other textures.
        Texture2D plusTexture;
        Texture2D padFaceTexture;

        Dictionary<int, Texture2D> m_KeyCode2Texture = new Dictionary<int, Texture2D>();

        InputComponent m_InputComponent;

        // This is the master list of moves in logical order. This array is kept
        // around in order to draw the move list on the screen in this order.
        Move[] moves;
        // The move list used for move detection at runtime.
        MoveManager moveManager;

        public InputSequenceSampleGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            m_InputComponent = new InputComponent(this);

            ButtonConfiguration ButtonConfiguration = new ButtonConfiguration();
            //Punch Light
            ButtonMapper map = new ButtonMapper();
            map.Buttons = Buttons.A;
            map.Key = Keys.Q;
            map.Name = "PL";
            ButtonConfiguration.AddButton((int)ButtonCode.PL, map);
            //Punch Medium
            map = new ButtonMapper();
            map.Buttons = Buttons.B;
            map.Key = Keys.B;
            map.Name = "PM";
            ButtonConfiguration.AddButton((int)ButtonCode.PM, map);
            //Punch High
            map = new ButtonMapper();
            map.Buttons = Buttons.RightShoulder;
            map.Key = Keys.B;
            map.Name = "PH";
            ButtonConfiguration.AddButton((int)ButtonCode.PH, map);
            //Kick Light
            map = new ButtonMapper();
            map.Buttons = Buttons.X;
            map.Key = Keys.Q;
            map.Name = "KL";
            ButtonConfiguration.AddButton((int)ButtonCode.KL, map);
            //Kick Medium
            map = new ButtonMapper();
            map.Buttons = Buttons.Y;
            map.Key = Keys.B;
            map.Name = "KM";
            ButtonConfiguration.AddButton((int)ButtonCode.KM, map);
            //Kick High
            map = new ButtonMapper();
            map.Buttons = Buttons.LeftShoulder;
            map.Key = Keys.B;
            map.Name = "KH";
            ButtonConfiguration.AddButton((int)ButtonCode.KH, map);
            //Left
            map = new ButtonMapper();
            map.Buttons = Buttons.LeftThumbstickLeft;
            map.Key = Keys.Left;
            map.Name = "Left";
            ButtonConfiguration.AddButton((int)ButtonCode.Left, map);
            //Right
            map = new ButtonMapper();
            map.Buttons = Buttons.LeftThumbstickRight;
            map.Key = Keys.Right;
            map.Name = "Right";
            ButtonConfiguration.AddButton((int)ButtonCode.Right, map);
            //Up
            map = new ButtonMapper();
            map.Buttons = Buttons.LeftThumbstickUp;
            map.Key = Keys.Up;
            map.Name = "Up";
            ButtonConfiguration.AddButton((int)ButtonCode.Up, map);
            //Down
            map = new ButtonMapper();
            map.Buttons = Buttons.LeftThumbstickDown;
            map.Key = Keys.Down;
            map.Name = "Down";
            ButtonConfiguration.AddButton((int)ButtonCode.Down, map);

            m_InputComponent.SetCurrentConfiguration(ButtonConfiguration);

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            spriteFont = Content.Load<SpriteFont>("Font");

            // Load direction textures.
            upTexture = Content.Load<Texture2D>("Up");
            downTexture = Content.Load<Texture2D>("Down");
            leftTexture = Content.Load<Texture2D>("Left");
            rightTexture = Content.Load<Texture2D>("Right");
            upLeftTexture = Content.Load<Texture2D>("UpLeft");
            upRightTexture = Content.Load<Texture2D>("UpRight");
            downLeftTexture = Content.Load<Texture2D>("DownLeft");
            downRightTexture = Content.Load<Texture2D>("DownRight");

            // Load button textures.
            aButtonTexture = Content.Load<Texture2D>("A");
            bButtonTexture = Content.Load<Texture2D>("B");
            xButtonTexture = Content.Load<Texture2D>("X");
            yButtonTexture = Content.Load<Texture2D>("Y");

            // Load other textures.
            plusTexture = Content.Load<Texture2D>("Plus");
            padFaceTexture = Content.Load<Texture2D>("PadFace");


            m_KeyCode2Texture.Add((int)ButtonCode.PH, aButtonTexture);
            m_KeyCode2Texture.Add((int)ButtonCode.PM, bButtonTexture);
            m_KeyCode2Texture.Add((int)ButtonCode.KL, xButtonTexture);
            m_KeyCode2Texture.Add((int)ButtonCode.KM, yButtonTexture);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();

            DrawInput(m_InputComponent.GetInputManager(PlayerIndex.One), Vector2.One);

            spriteBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the input buffer and most recently fired action for a given player.
        /// </summary>
        private void DrawInput(InputManager inputManager_, Vector2 position)
        {
            /*Move move = playerMoves[i];

            if (move != null)
            {
                DrawString(move.Name,
                    new Vector2(position.X + textSize.X, position.Y), Color.Red);
            }

            // Draw the player's input buffer.
            position.Y += textSize.Y;*/
            DrawSequence(inputManager_.Buffer, position);
        }

        /// <summary>
        /// Draws a string with a subtle drop shadow.
        /// </summary>
        private void DrawString(string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(spriteFont, text,
                new Vector2(position.X, position.Y + 1), Color.Black);
            spriteBatch.DrawString(spriteFont, text,
                new Vector2(position.X, position.Y), color);
        }

        /// <summary>
        /// Calculates the size of what would be drawn by a call to DrawSequence.
        /// </summary>
        private Vector2 MeasureSequence(IEnumerable<InputManager.KeyStateFrame> sequence)
        {
            float width = 0.0f;
           /* foreach (InputManager.KeyStateFrame buttons in sequence)
            {
                width += MeasureButtons(buttons).X;
            }*/
            return new Vector2(width, padFaceTexture.Height);
        }

        /// <summary>
        /// Draws a horizontal series of input steps in a sequence.
        /// </summary>
        private void DrawSequence(IEnumerable<InputManager.KeyStateFrame> sequence, Vector2 position)
        {
            foreach (InputManager.KeyStateFrame buttons in sequence)
            {
                position.X += DrawButtons(buttons, position);
                //MeasureButtons(buttons).X;
            }
        }

        /// <summary>
        /// Calculates the size of what would be drawn by a call to DrawButtons.
        /// </summary>
        private Vector2 MeasureButtons(int code_)
        {
            /*Buttons direction = Direction.FromButtons(buttons);

            float width;

            // If buttons has a direction,
            if (direction > 0)
            {
                width = GetDirectionTexture(direction).Width;
                // If buttons has at least one non-directional button,
                if ((buttons & ~direction) > 0)
                {
                    width += plusTexture.Width + padFaceTexture.Width;
                }
            }
            else
            {
                width = padFaceTexture.Width;
            }

            return new Vector2(width, padFaceTexture.Height);*/
            return Vector2.Zero;
        }

        /// <summary>
        /// Draws the combined state of a set of buttons flags. The rendered output
        /// looks like a directional arrow, a group of buttons, or both concatenated
        /// with a plus sign operator.
        /// </summary>
        private float DrawButtons(InputManager.KeyStateFrame buttons, Vector2 position)
        {
            float res = 0.0f;

            bool draw = false;

            for (int i = 0; i < buttons.KeysState.Count; i++)
            {
                if (buttons.KeysState[i].State == ButtonState.Pressed)
                {
                    //if (m_KeyCode2Texture.ContainsKey(buttons.KeysState[i].Key) == true)
                    {
                        //string msg = buttons.KeysState[i].Key.ToString() + " (" + buttons.KeysState[i].Time + ")" + Enum.GetName(typeof(ButtonState), buttons.KeysState[i].State);
                        string msg = buttons.KeysState[i].Key.ToString();

                        spriteBatch.DrawString(
                            spriteFont, msg,
                            position, Color.White);
                        position.Y += spriteFont.MeasureString(msg).Y + 5;

                        draw = true;

                        if (i < buttons.KeysState.Count - 1)
                        {
                            spriteBatch.DrawString(spriteFont, "+", position, Color.White);
                            position.Y += spriteFont.MeasureString("+").Y + 5;
                            //res += spriteFont.MeasureString("+").X;
                        }

                        /*spriteBatch.Draw(m_KeyCode2Texture[buttons.KeysState[i].Key], position, Color.White);
                        position.X += m_KeyCode2Texture[buttons.KeysState[i].Key].Width;
                        res += m_KeyCode2Texture[buttons.KeysState[i].Key].Width;

                        if (i < buttons.KeysState.Count - 1)
                        {
                            spriteBatch.Draw(plusTexture, position, Color.White);
                            position.X += plusTexture.Width;
                            res += plusTexture.Width;
                        }*/
                    }
                }

                /*string msg = buttons.KeysState[i].Key.ToString() + " (" + buttons.KeysState[i].Time + ")" + Enum.GetName(typeof(ButtonState), buttons.KeysState[i].State);

                spriteBatch.DrawString(
                    spriteFont, msg, 
                    position, Color.White);
                position.Y += spriteFont.MeasureString(msg).Y + 5;*/
            }

            if (draw == true)
            {
                res += 30;
            }

            return res;

            // Get the texture to draw for the direction.
            /*Buttons direction = Direction.FromButtons(buttons);
            Texture2D directionTexture = GetDirectionTexture(direction);

            // If there is a direction, draw it.
            if (directionTexture != null)
            {
                spriteBatch.Draw(directionTexture, position, Color.White);
                position.X += directionTexture.Width;
            }

            // If any non-direction button is pressed,
            if ((buttons & ~direction) > 0)
            {
                // Draw a plus if both a direction and one more more buttons is pressed.
                if (directionTexture != null)
                {
                    spriteBatch.Draw(plusTexture, position, Color.White);
                    position.X += plusTexture.Width;
                }

                // Draw a gamepad with all inactive buttons in the background.
                spriteBatch.Draw(padFaceTexture, position, Color.White);

                // Draw each active button over the inactive game pad face.
                if ((buttons & Buttons.A) > 0)
                {
                    spriteBatch.Draw(aButtonTexture, position, Color.White);
                }
                if ((buttons & Buttons.B) > 0)
                {
                    spriteBatch.Draw(bButtonTexture, position, Color.White);
                }
                if ((buttons & Buttons.X) > 0)
                {
                    spriteBatch.Draw(xButtonTexture, position, Color.White);
                }
                if ((buttons & Buttons.Y) > 0)
                {
                    spriteBatch.Draw(yButtonTexture, position, Color.White);
                }
            }*/
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework;
using CasaEngineCommon.Helper;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.Math.Shape2D;
using CasaEngine.Math;
using Microsoft.Xna.Framework.Input;
using Editor.Tools.Graphics2D;
using System.Windows.Forms;
using CasaEngine.Editor.UndoRedo;
using Editor.UndoRedo;
using System.ComponentModel;
using CasaEngine.Editor.Manipulation;
using CasaEngine.Assets.Graphics2D;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Editor.Game
{
    public class Sprite2DEditorComponent
        : CasaEngine.Game.DrawableGameComponent
    {

        /// <summary>
        /// 
        /// </summary>
        public enum EditonMode
        {
            HotSpot,
            Collision,
            Socket
        }



        /// <summary>
        /// Copy of the original Sprite2D which will be edited.
        /// </summary>
        Sprite2D m_CurrentSprite2D;
        Sprite2D m_OriginalSPrite2D;
        Vector2 m_SpritePosition, m_Zoom;
        SpriteBatch m_SpriteBatch;
        Line2DRenderer m_Line2DRenderer;
        int m_CurrentCollisonIndex = -1, m_CurrentSocketIndex = -1;
        Shape2DManipulator m_CurrentShape2DManipulator;
        string m_ObjectPath;

        MouseWindowed m_Mouse;
        Point m_MouseLeftDownPosition = new Point();
        Point m_MouseRightDownPosition = new Point();
        bool m_MouseLeftDown = false;
        bool m_MouseRightDown = false;

        Sprite2D m_ChangeCurrentSprite;
        bool m_NeedChangeSprite2D = false;

        public volatile bool ShiftKeyPressed, ControlKeyPressed;

        EditonMode m_Mode = EditonMode.HotSpot;

        //used to manipulate point (hotspot, socket, ...)
        Anchor m_Anchor;



        /// <summary>
        /// 
        /// </summary>
        public EditonMode Mode
        {
            get { return m_Mode; }
            set
            {
                m_Mode = value;
                int w = 11, h = 11;

                if (m_Mode == EditonMode.HotSpot)
                {
                    CreateAnchor((int)m_SpritePosition.X, (int)m_SpritePosition.Y);
                }
                else if (m_Mode == EditonMode.Socket)
                {
                    if (m_CurrentSocketIndex != -1)
                    {
                        Vector2 v = m_CurrentSprite2D.GetSocketByIndex(m_CurrentSocketIndex);
                        CreateAnchor((int)(m_SpritePosition.X + v.X), (int)(m_SpritePosition.Y + v.Y));
                    }
                    else
                    {
                        m_Anchor = null;
                    }
                }
                else
                {
                    m_Anchor = null;
                }
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int CurrentCollisonIndex
        {
            get { return m_CurrentCollisonIndex; }
            set
            {
                if (m_CurrentSprite2D != null && m_Mode == EditonMode.Collision)
                {
                    /*if (m_CurrentCollisonIndex >= 0 && m_CurrentCollisonIndex < m_CurrentSprite2D.Collisions.Length
                        && m_CurrentShape2DManipulator != null)
                    {
                        m_CurrentSprite2D.SetCollisionAt(m_CurrentCollisonIndex, m_CurrentShape2DManipulator.Shape2DObject);
                    }*/

                    if (value >= 0 && value < m_CurrentSprite2D.Collisions.Length)
                    {
                        m_CurrentCollisonIndex = value;

                        switch (m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex].Shape2DType)
                        {
                            case Shape2DType.Circle:
                                m_CurrentShape2DManipulator = new Shape2DManipulatorCircle((ShapeCircle)m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex]);
                                break;

                            case Shape2DType.Line:
                                m_CurrentShape2DManipulator = new Shape2DManipulatorLine((ShapeLine)m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex]);
                                break;

                            case Shape2DType.Polygone:
                                m_CurrentShape2DManipulator = new Shape2DManipulatorPolygone((ShapePolygone)m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex]);
                                break;

                            case Shape2DType.Rectangle:
                                m_CurrentShape2DManipulator = new Shape2DManipulatorRectangle((ShapeRectangle)m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex]);
                                break;

                            default:
                                throw new InvalidOperationException("Sprite2DEditorForm.listBoxCollision_SelectedIndexChanged() : Shape2DType '" + Enum.GetName(typeof(Shape2DType), m_CurrentSprite2D.Collisions[m_CurrentCollisonIndex].Shape2DType) + "' not supported");
                        }

                        m_CurrentShape2DManipulator.Sprite2DEditorComponent = this;
                        m_CurrentShape2DManipulator.UndoRedoManager = UndoRedoManager;
                        m_CurrentShape2DManipulator.UndoRedoManager.EventCommandDone += new EventHandler(OnEventCommandDone);
                        m_CurrentShape2DManipulator.CursorChanged += new EventHandler(OnCursorChanged);

                        m_CurrentShape2DManipulator.Offset = new Vector2(
                            (int)m_SpritePosition.X - m_CurrentSprite2D.HotSpot.X,
                            (int)m_SpritePosition.Y - m_CurrentSprite2D.HotSpot.Y);
                    }
                    else
                    {
                        m_CurrentShape2DManipulator = null;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int CurrentSocketIndex
        {
            get { return m_CurrentSocketIndex; }
            set
            {
                if (m_CurrentSprite2D != null
                    && m_Mode == EditonMode.Socket)
                {
                    m_CurrentSocketIndex = value;

                    if (m_CurrentSocketIndex != -1)
                    {
                        Vector2 v = m_CurrentSprite2D.GetSocketByIndex(m_CurrentSocketIndex);
                        CreateAnchor((int)(m_SpritePosition.X + v.X), (int)(m_SpritePosition.Y + v.Y));
                    }
                    else
                    {
                        m_Anchor = null;
                    }
                }
            }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Sprite2D CurrentSprite2D
        {
            get { return m_CurrentSprite2D; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Shape2DManipulator CurrentShape2DManipulator
        {
            get { return m_CurrentShape2DManipulator; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public UndoRedoManager UndoRedoManager
        {
            get;
            set;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        internal Sprite2DEditorComponent(CustomGameEditor game_)
            : base(game_)
        {
            game_.Components.Add(this);

            m_Zoom = Vector2.One;
        }




        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            m_SpriteBatch = new SpriteBatch(Game.GraphicsDevice);
            m_Line2DRenderer = new Line2DRenderer();
            m_Line2DRenderer.Init(Game.GraphicsDevice);
            m_Mouse = new MouseWindowed(Game.GraphicsDevice.PresentationParameters.DeviceWindowHandle);

            base.LoadContent();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void UnloadContent()
        {
            base.UnloadContent();

            if (m_CurrentSprite2D != null
                    && m_CurrentSprite2D.Texture2D != null)
            {
                m_CurrentSprite2D.Texture2D.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            ChangeCurrentSprite2DIfNeeded();

            if (m_MouseLeftDown == true)
            {
                int delatX = m_MouseLeftDownPosition.X - m_Mouse.X;
                int delatY = m_MouseLeftDownPosition.Y - m_Mouse.Y;
            }

            if (m_MouseRightDown == true)
            {
                int delatX = m_MouseRightDownPosition.X - m_Mouse.X;
                int delatY = m_MouseRightDownPosition.Y - m_Mouse.Y;

                m_SpritePosition.X -= delatX;
                m_SpritePosition.Y -= delatY;

                //if (Mode == EditonMode.Collision)
                //{
                if (m_CurrentShape2DManipulator != null
                && m_CurrentSprite2D != null)
                {
                    m_CurrentShape2DManipulator.Offset = new Vector2(
                        m_SpritePosition.X - m_CurrentSprite2D.HotSpot.X,
                        m_SpritePosition.Y - m_CurrentSprite2D.HotSpot.Y);
                }
                //}                              
            }

            //Left
            if (m_Mouse.LeftButton == true)
            {
                m_MouseLeftDownPosition.X = m_Mouse.X;
                m_MouseLeftDownPosition.Y = m_Mouse.Y;
            }

            m_MouseLeftDown = m_Mouse.LeftButton;

            //Right
            if (m_Mouse.RightButton == true)
            {
                m_MouseRightDownPosition.X = m_Mouse.X;
                m_MouseRightDownPosition.Y = m_Mouse.Y;
            }

            m_MouseRightDown = m_Mouse.RightButton;

            switch (Mode)
            {
                case EditonMode.HotSpot:

                    break;

                case EditonMode.Collision:
                    if (m_CurrentShape2DManipulator != null)
                    {
                        m_CurrentShape2DManipulator.Update(m_Mouse.GetState(), ShiftKeyPressed, ControlKeyPressed);
                    }
                    break;

                case EditonMode.Socket:

                    break;
            }

            if (m_Anchor != null)
            {
                m_Anchor.Update(m_Mouse.GetState(), ShiftKeyPressed, ControlKeyPressed, false);
            }

            base.Update(gameTime);

            m_Mouse.ScrollWheelValueReset();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            if (m_CurrentSprite2D != null
                && m_CurrentSprite2D.Texture2D != null)
            {
                Vector2 hotspot = Vector2.Zero;
                hotspot.X = m_CurrentSprite2D.HotSpot.X;
                hotspot.Y = m_CurrentSprite2D.HotSpot.Y;
                Rectangle? rect = m_CurrentSprite2D.PositionInTexture;

                m_SpriteBatch.Begin(SpriteSortMode.Immediate,
                    BlendState.NonPremultiplied, //AlphaBlend need texture to be compiled with some options
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise);

                m_SpriteBatch.Draw(m_CurrentSprite2D.Texture2D, m_SpritePosition, rect, Color.White,
                    0.0f, hotspot, m_Zoom, SpriteEffects.None, 0.5f);

                switch (Mode)
                {
                    case EditonMode.HotSpot:
                        DrawHotSpot();
                        break;

                    case EditonMode.Collision:
                        DrawShape2D();
                        break;

                    case EditonMode.Socket:
                        DrawSocket();
                        break;
                }

                if (m_Anchor != null)
                {
                    m_Anchor.Draw(m_Line2DRenderer, m_SpriteBatch, Color.Green);
                }

                m_SpriteBatch.End();
            }

            base.Draw(gameTime);
        }


        /// <summary>
        /// 
        /// </summary>
        private void DrawHotSpot()
        {
            if (m_CurrentSprite2D != null)
            {
                float x = m_SpritePosition.X;
                float y = m_SpritePosition.Y;
                m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                    new Vector2(x - 6, y), new Vector2(x + 5, y));
                m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                    new Vector2(x, y - 5), new Vector2(x, y + 6));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DrawShape2D()
        {
            if (m_CurrentShape2DManipulator != null)
            {
                m_CurrentShape2DManipulator.Draw(m_Line2DRenderer, m_SpriteBatch);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DrawSocket()
        {
            if (m_CurrentSprite2D != null
                && m_CurrentSocketIndex != -1)
            {
                List<KeyValuePair<string, Vector2>> list = m_CurrentSprite2D.GetSockets();
                string name = list[m_CurrentSocketIndex].Key;
                Vector2 socketPos = list[m_CurrentSocketIndex].Value;

                float x = m_SpritePosition.X + socketPos.X;
                float y = m_SpritePosition.Y + socketPos.Y;
                m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                    new Vector2(x - 6, y), new Vector2(x + 5, y));
                m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                    new Vector2(x, y - 5), new Vector2(x, y + 6));

                //m_SpriteBatch.DrawString(, name, new Vector2(x + 10, y), Color.Yellow);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnEventCommandDone(object sender, EventArgs e)
        {
            if (m_CurrentShape2DManipulator != null)
            {
                m_CurrentShape2DManipulator.Offset = new Vector2(
                        (int)m_SpritePosition.X - m_CurrentSprite2D.HotSpot.X,
                        (int)m_SpritePosition.Y - m_CurrentSprite2D.HotSpot.Y);
            }
        }

        /// <summary>
        /// Sprite2d.Collisions[CurrentCollisonIndex] = current Shape2DObject
        /// </summary>
        public void ApplyCollisionChanges()
        {
            if (m_CurrentSprite2D != null
                   && m_Mode == EditonMode.Collision)
            {
                if (m_CurrentCollisonIndex >= 0 && m_CurrentCollisonIndex < m_CurrentSprite2D.Collisions.Length
                    && m_CurrentShape2DManipulator != null)
                {
                    m_CurrentSprite2D.SetCollisionAt(m_CurrentCollisonIndex, m_CurrentShape2DManipulator.Shape2DObject);
                }
            }
        }

        /// <summary>
        /// Use to change the current Sprite2D in
        /// the thread of XNA
        /// </summary>
        private void ChangeCurrentSprite2DIfNeeded()
        {
            if (m_NeedChangeSprite2D == false)
            {
                return;
            }

            if (m_CurrentSprite2D != null
                    && m_ChangeCurrentSprite != null)
            {
                if (m_CurrentSprite2D.Texture2D.Equals(m_ChangeCurrentSprite.Texture2D) == false)
                {
                    m_CurrentSprite2D.Texture2D.Dispose();
                }
            }

            m_OriginalSPrite2D = m_ChangeCurrentSprite;
            m_CurrentSprite2D = new Sprite2D(m_ChangeCurrentSprite);
            m_SpritePosition.X = (Game.GraphicsDevice.Viewport.Width - m_CurrentSprite2D.PositionInTexture.Width) / 2;
            m_SpritePosition.Y = (Game.GraphicsDevice.Viewport.Height - m_CurrentSprite2D.PositionInTexture.Height) / 2;

            m_CurrentSprite2D.LoadTextureFile(Game.GraphicsDevice);

            m_CurrentShape2DManipulator = null;
            m_CurrentSocketIndex = -1;
            m_CurrentCollisonIndex = -1;
            m_Anchor = null;

            m_NeedChangeSprite2D = false;
            m_ChangeCurrentSprite = null;

            Mode = m_Mode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectPath_"></param>
        /// <param name="sprite2D_"></param>
        public void SetCurrentSprite2D(string objectPath_, Sprite2D sprite2D_)
        {
            m_ChangeCurrentSprite = sprite2D_;
            m_ObjectPath = objectPath_;
            m_NeedChangeSprite2D = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsSprite2DChange()
        {
            if (m_CurrentSprite2D != null)
            {
                return !(m_CurrentSprite2D.CompareTo(m_OriginalSPrite2D));
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ApplySprite2DChanges()
        {
            if (m_CurrentSprite2D != null)
            {
                Engine.Instance.ObjectManager.Replace(m_ObjectPath, m_CurrentSprite2D);
            }
        }

        delegate void SetCursorDelegate(Cursor c_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnCursorChanged(object sender, EventArgs args)
        {
            if (sender is Cursor)
            {
                Cursor c = sender as Cursor;
                Game.Control.Invoke(new SetCursorDelegate(SetCursor), c);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c_"></param>
        private void SetCursor(Cursor c_)
        {
            Game.Control.Cursor = c_;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x_"></param>
        /// <param name="y_"></param>
        private void CreateAnchor(int x_, int y_)
        {
            m_Anchor = new Anchor(x_, y_, 10, 10);
            m_Anchor.CursorChanged += new EventHandler(Point_CursorChanged);
            m_Anchor.LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(Point_LocationChanged);
            m_Anchor.StartManipulating += new EventHandler(OnStartManipulating);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnStartManipulating(object sender, EventArgs e)
        {
            ICommand command = null;

            if (m_Mode == EditonMode.HotSpot)
            {
                command = new UndoRedoHotSpotCommand(m_CurrentSprite2D.HotSpot);
            }
            else if (m_Mode == EditonMode.Socket)
            {
                command = new UndoRedoSocketCommand(m_CurrentSprite2D.GetSockets()[m_CurrentSocketIndex], m_CurrentSocketIndex);
            }

            if (command != null)
            {
                UndoRedoManager.Add(command, this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Point_LocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            if (m_Mode == EditonMode.HotSpot)
            {
                if (m_CurrentSprite2D != null)
                {
                    m_CurrentSprite2D.HotSpot = new Point(m_CurrentSprite2D.HotSpot.X + e.OffsetX, m_CurrentSprite2D.HotSpot.Y + e.OffsetY);
                    m_SpritePosition.X += e.OffsetX;
                    m_SpritePosition.Y += e.OffsetY;
                }
            }
            else if (m_Mode == EditonMode.Socket)
            {
                if (m_CurrentSprite2D != null
                    && m_CurrentSocketIndex != -1)
                {
                    Vector2 v = m_CurrentSprite2D.GetSocketByIndex(m_CurrentSocketIndex);
                    v.X += e.OffsetX;
                    v.Y += e.OffsetY;
                    m_CurrentSprite2D.ModifySocket(m_CurrentSocketIndex, v);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Point_CursorChanged(object sender, EventArgs e)
        {
            OnCursorChanged(Cursors.SizeAll, EventArgs.Empty);
        }


    }
}

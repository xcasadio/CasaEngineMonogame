using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Editor.Manipulation;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Editor.UndoRedo;
using CasaEngine.Core_Systems.Math.Shape2D;
using Editor.UndoRedo;
using Editor.Game;
using Color = Microsoft.Xna.Framework.Color;

namespace Editor.Tools.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class Shape2DManipulator
    {
        static public readonly int AnchorHeight = 7, AnchorWidth = 7;

        protected List<Anchor> m_AnchorList = new();
        public event EventHandler CursorChanged;
        Shape2DObject m_Shape2DObject;
        Vector2 m_Offset;


        private volatile object m_SyncRoot = new();

        /// <summary>
        /// Use to synchronize
        /// </summary>
        public object SyncRoot
        {
            get { return m_SyncRoot; }
        }

        /// <summary>
        /// Gets/Sets lock(SyncRoot)
        /// </summary>
        public Vector2 Offset
        {
            get { return m_Offset; }
            set
            {
                m_Offset = value;

                lock (SyncRoot)
                {
                    foreach (Anchor a in m_AnchorList)
                    {
                        a.Offset = m_Offset;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public UndoRedoManager UndoRedoManager
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public Sprite2DEditorComponent Sprite2DEditorComponent
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets lock(SyncRoot)
        /// </summary>
        public Shape2DObject Shape2DObject
        {
            get { return m_Shape2DObject; }
            set
            {
                lock (SyncRoot)
                {
                    m_Shape2DObject = value;
                    CreateAnchor();
                }
            }
        }

        /// <summary>
        /// lock(SyncRoot)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void Move(int x, int y)
        {
            lock (SyncRoot)
            {
                foreach (Anchor a in m_AnchorList)
                {
                    a.SetLocationOffSet(x, y, false);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shape2DObject_"></param>
        protected Shape2DManipulator(Shape2DObject shape2DObject_)
        {
            Shape2DObject = shape2DObject_;
        }

        /// <summary>
        /// 
        /// </summary>
        protected abstract void CreateAnchor();

        /// <summary>
        /// lock(SyncRoot)
        /// </summary>
        protected void InitializeAnchorsEvent()
        {
            lock (SyncRoot)
            {
                foreach (Anchor a in m_AnchorList)
                {
                    a.StartManipulating += new EventHandler(OnStartManipulating);
                    a.CursorChanged += new EventHandler(OnCursorChanged);
                    a.Offset = m_Offset;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnStartManipulating(object sender, EventArgs e)
        {
            UndoRedoShape2DObjectCommand command = new UndoRedoShape2DObjectCommand(Shape2DObject);
            UndoRedoManager.Add(command, Sprite2DEditorComponent);
        }

        /// <summary>
        /// lock(SyncRoot)
        /// </summary>
        /// <param name="mouseState_"></param>
        /// <param name="shiftPressed_"></param>
        /// <param name="controlPressed_"></param>
        public virtual void Update(MouseState mouseState_, bool shiftPressed_, bool controlPressed_)
        {
            lock (SyncRoot)
            {
                foreach (Anchor a in m_AnchorList)
                {
                    a.Update(mouseState_, shiftPressed_, controlPressed_, false);
                }
            }
        }

        /// <summary>
        /// lock(SyncRoot)
        /// </summary>
        /// <param name="line2DRenderer_"></param>
        /// <param name="spriteBatch_"></param>
        /// <param name="position_"></param>
        public virtual void Draw(Line2DRenderer line2DRenderer_, SpriteBatch spriteBatch_)
        {
            lock (SyncRoot)
            {
                foreach (Anchor a in m_AnchorList)
                {
                    a.Draw(line2DRenderer_, spriteBatch_, Color.Blue);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCursorChanged(object sender, EventArgs e)
        {
            if (CursorChanged != null)
            {
                CursorChanged.Invoke(sender, EventArgs.Empty);
            }
        }
    }
}

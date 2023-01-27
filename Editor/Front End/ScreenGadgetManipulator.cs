using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.FrontEnd.Screen.Gadget;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Editor.Manipulation;
using Microsoft.Xna.Framework;

namespace Editor.FrontEnd
{
    /// <summary>
    /// 
    /// </summary>
    public class ScreenGadgetManipulator
        : ScreenGadget
    {
        #region Fields

        private Anchor[] m_Anchors = new Anchor[9];
        public bool Selected = false;
        private bool m_Catch = true;
        Vector2 mouseStart;
        bool m_MousePressed = false;

        private ScreenGadget m_ScreenGadget;

        #endregion

        #region Properties

        /// <summary>
        /// Gets
        /// </summary>
        public ScreenGadget ScreenGadget
        {
            get { return m_ScreenGadget; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        new public string Name
        {
            get { return m_ScreenGadget.Name; }
            set { m_ScreenGadget.Name = value; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gadget_"></param>
        public ScreenGadgetManipulator(ScreenGadget gadget_)
            : base(gadget_.Name)
        {
            if (gadget_ == null)
            {
                throw new ArgumentNullException("ScreenGadgetManipulator() : ScreenGadget is null");
            }

            m_ScreenGadget = gadget_;
            int l = 7;
            
            //TopLeft
            m_Anchors[0] = new Anchor((int)m_ScreenGadget.Location.X, (int)m_ScreenGadget.Location.Y, l, l);
            m_Anchors[0].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_TopLeft);

            //middle top
            m_Anchors[1] = new Anchor((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2, (int)m_ScreenGadget.Location.Y, l, l);
            m_Anchors[1].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_MiddleTop);
            m_Anchors[1].MoveOnX = false;

            //Top right
            m_Anchors[2] = new Anchor((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width, (int)m_ScreenGadget.Location.Y, l, l);
            m_Anchors[2].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_TopRight);

            //Middle right
            m_Anchors[3] = new Anchor((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2, l, l);
            m_Anchors[3].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_MiddleRight);
            m_Anchors[3].MoveOnY = false;

            //Bottom right
            m_Anchors[4] = new Anchor((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height, l, l);
            m_Anchors[4].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_BottomRight);

            //Middle bottom
            m_Anchors[5] = new Anchor((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height, l, l);
            m_Anchors[5].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_MiddleBottom);
            m_Anchors[5].MoveOnX = false;

            //Bottom left
            m_Anchors[6] = new Anchor((int)m_ScreenGadget.Location.X, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height, l, l);
            m_Anchors[6].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_BottomLeft);

            //Middle left
            m_Anchors[7] = new Anchor((int)m_ScreenGadget.Location.X, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2, l, l);
            m_Anchors[7].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged_MiddleLeft);
            m_Anchors[7].MoveOnY = false;

            //main
            m_Anchors[8] = new Anchor((int)m_ScreenGadget.Location.X, (int)m_ScreenGadget.Location.Y, m_ScreenGadget.Width, m_ScreenGadget.Height);
            m_Anchors[8].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ScreenGadgetManipulator_LocationChanged);
            
            for (int i = 0; i < 8; i++)
            {
                m_Anchors[8].LinkWithAnchor(m_Anchors[i]);
            }
        }

        void ScreenGadgetManipulator_LocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(e.OffsetX, e.OffsetY);

            for (int i = 0; i < 8; i++)
            {
                m_Anchors[i].SetLocationOffSet(e.OffsetX, e.OffsetY, false);
            }
        }

        void ScreenGadgetManipulator_LocationChanged_TopLeft(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(e.OffsetX, e.OffsetY);
            m_ScreenGadget.Width -= e.OffsetX;
            m_ScreenGadget.Height -= e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_MiddleTop(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(0, e.OffsetY);
            m_ScreenGadget.Height -= e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_TopRight(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(0, e.OffsetY);
            m_ScreenGadget.Width += e.OffsetX;
            m_ScreenGadget.Height -= e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_MiddleRight(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Width += e.OffsetX;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_BottomRight(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Width += e.OffsetX;
            m_ScreenGadget.Height += e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_MiddleBottom(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Height += e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_BottomLeft(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(e.OffsetX, 0);
            m_ScreenGadget.Width -= e.OffsetX;
            m_ScreenGadget.Height += e.OffsetY;

            UpdateAnchorPosition();
        }

        void ScreenGadgetManipulator_LocationChanged_MiddleLeft(object sender, AnchorLocationChangedEventArgs e)
        {
            m_ScreenGadget.Location += new Vector2(e.OffsetX, e.OffsetY);
            m_ScreenGadget.Width -= e.OffsetX;

            UpdateAnchorPosition();
        }

        private void UpdateAnchorPosition()
        {
            //TopLeft
            m_Anchors[0].SetLocation((int)m_ScreenGadget.Location.X - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y - m_Anchors[0].Height / 2, false);

            //middle top
            m_Anchors[1].SetLocation((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2 - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y - m_Anchors[0].Height / 2, false);

            //Top right
            m_Anchors[2].SetLocation((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y - m_Anchors[0].Height / 2, false);

            //Middle right
            m_Anchors[3].SetLocation((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2 - m_Anchors[0].Height / 2, false);

            //Bottom right
            m_Anchors[4].SetLocation((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height - m_Anchors[0].Height / 2, false);

            //Middle bottom
            m_Anchors[5].SetLocation((int)m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2 - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height - m_Anchors[0].Height / 2, false);

            //Bottom left
            m_Anchors[6].SetLocation((int)m_ScreenGadget.Location.X - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height - m_Anchors[0].Height / 2, false);

            //Middle left
            m_Anchors[7].SetLocation((int)m_ScreenGadget.Location.X - m_Anchors[0].Width / 2, (int)m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2 - m_Anchors[0].Height / 2, false);

            //main
            m_Anchors[8].SetLocation((int)m_ScreenGadget.Location.X, (int)m_ScreenGadget.Location.Y, false);
            m_Anchors[8].Width = m_ScreenGadget.Width;
            m_Anchors[8].Height = m_ScreenGadget.Height;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public override void Update(float elapsedTime_)
        {
            MouseState mouseState = Mouse.GetState();

            foreach (Anchor a in m_Anchors)
            {
                a.Update(mouseState, false, false, false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public override void DrawGadget(float elapsedTime_)
        {
            /*Renderer2DComponent.AddBox(m_ScreenGadget.Location.X, m_ScreenGadget.Location.Y, m_ScreenGadget.Width, m_ScreenGadget.Height, Color.Black);

            //anchors
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X - 3, m_ScreenGadget.Location.Y - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X + m_ScreenGadget.Width - 3, m_ScreenGadget.Location.Y - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X - 3, m_ScreenGadget.Location.Y + m_ScreenGadget.Height - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X + m_ScreenGadget.Width - 3, m_ScreenGadget.Location.Y + m_ScreenGadget.Height - 3, 6, 6, Color.Black);

            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2 - 3, m_ScreenGadget.Location.Y - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X + m_ScreenGadget.Width / 2 - 3, m_ScreenGadget.Location.Y + m_ScreenGadget.Height - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X - 3, m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2 - 3, 6, 6, Color.Black);
            Renderer2DComponent.AddBox(m_ScreenGadget.Location.X + m_ScreenGadget.Width - 3, m_ScreenGadget.Location.Y + m_ScreenGadget.Height / 2 - 3, 6, 6, Color.Black);
            */
            foreach (Anchor a in m_Anchors)
            {
                a.Draw(Renderer2DComponent);
            }

            m_ScreenGadget.DrawGadget(elapsedTime_);
        }

        #endregion
    }
}

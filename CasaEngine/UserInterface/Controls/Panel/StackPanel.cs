
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using Microsoft.Xna.Framework;
#endregion

namespace XNAFinalEngine.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    public class StackPanel : Container
    {
        #region Variables

        private Orientation m_Orientation;

        #endregion

        #region Properties

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public XNAFinalEngine.UserInterface.Orientation Orientation
        {
            get { return m_Orientation; }
            set { m_Orientation = value; }
        }

        #endregion

        #region Constructor

        public StackPanel(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            this.m_Orientation = Orientation.Horizontal;
            Color = Color.Transparent;
        } // StackPanel

        #endregion

        #region Calculate Layout

        private void CalculateLayout()
        {
            int top = Top;
            int left = Left;

            foreach (Control c in ClientArea.ChildrenControls)
            {
                Margins m = c.DefaultDistanceFromAnotherControl;

                if (m_Orientation == Orientation.Vertical)
                {
                    top += m.Top;
                    c.Top = top;
                    top += c.Height;
                    top += m.Bottom;
                    c.Left = left;
                }

                if (m_Orientation == Orientation.Horizontal)
                {
                    left += m.Left;
                    c.Left = left;
                    left += c.Width;
                    left += m.Right;
                    c.Top = top;
                }
            }
        } // CalculateLayout

        #endregion

        #region On Resize

        protected override void OnResize(ResizeEventArgs e)
        {
            CalculateLayout();
            base.OnResize(e);
        } // OnResize

        #endregion

    } // StackPanel
} //  XNAFinalEngine.UserInterface
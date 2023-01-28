
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;

namespace XNAFinalEngine.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    public class StackPanel : Container
    {

        private Orientation m_Orientation;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public XNAFinalEngine.UserInterface.Orientation Orientation
        {
            get { return m_Orientation; }
            set { m_Orientation = value; }
        }



        public StackPanel(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            this.m_Orientation = Orientation.Horizontal;
            Color = Color.Transparent;
        } // StackPanel



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



        protected override void OnResize(ResizeEventArgs e)
        {
            CalculateLayout();
            base.OnResize(e);
        } // OnResize


    } // StackPanel
} //  XNAFinalEngine.UserInterface
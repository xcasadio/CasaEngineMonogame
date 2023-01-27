
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
    /// Label.
    /// </summary>
    public class Label : Control
    {

        #region Variables

        /// <summary>
        /// Aligment. (None, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight).
        /// </summary>
        private Alignment alignment = Alignment.MiddleLeft;

        /// <summary>
        /// Ellipsis. Cut the text using "..." when doesn't fit.
        /// </summary>
        private bool ellipsis = true;

        #endregion

        #region Properties

        /// <summary>
        /// Aligment. (None, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight).
        /// </summary>
        public virtual Alignment Alignment
        {
            get { return alignment; }
            set { alignment = value; }
        } // Alignment

        /// <summary>
        /// Ellipsis. Cut the text using "..." when doesn't fit.
        /// </summary>
        public virtual bool Ellipsis
        {
            get { return ellipsis; }
            set { ellipsis = value; }
        } // Ellipsis

        #endregion

        #region Constructor

        /// <summary>
        /// Label.
        /// </summary>
        public Label(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Passive = true;
            Width = 64;
            Height = 16;
        } // Label

        #endregion

        #region Draw Control

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            SkinLayer skinLayer = SkinInformation.Layers[0];
            UserInterfaceManager.Renderer.DrawString(this, skinLayer, Text, rect, true, 0, 0, ellipsis);
        } // DrawControl

        #endregion

    } // Label
} // XNAFinalEngine.UserInterface

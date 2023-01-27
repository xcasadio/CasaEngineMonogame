
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Status Bar. It's just a container.
    /// </summary>
    public class StatusBar : Control
    {

        #region Constructor
    
        /// <summary>
        /// Status Bar. It's just a container.
        /// </summary>
        public StatusBar(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Left = 0;
            Top = 0;
            Width = 64;
            Height = 24;
            CanFocus = false;
        } // StatusBar

        #endregion

        #region Init Skin

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["StatusBar"]);
        } // InitSkin

        #endregion

    } // StatusBar
} // XNAFinalEngine.UserInterface
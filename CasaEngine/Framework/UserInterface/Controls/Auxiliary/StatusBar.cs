/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace CasaEngine.Framework.UserInterface.Controls.Auxiliary;

public class StatusBar : Control
{


    public StatusBar(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        Left = 0;
        Top = 0;
        Width = 64;
        Height = 24;
        CanFocus = false;
    } // StatusBar



    protected internal override void InitSkin()
    {
        base.InitSkin();
        SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["StatusBar"]);
    } // InitSkin


} // StatusBar
  // XNAFinalEngine.UserInterface
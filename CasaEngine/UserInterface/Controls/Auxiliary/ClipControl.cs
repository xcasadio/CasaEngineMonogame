
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class ClipControl : Control
    {


        public int AvailablePositionInsideControl
        {
            get
            {
                int max = 0;
                foreach (var childrenControl in ClientArea.ChildrenControls)
                {
                    if (childrenControl.Top + childrenControl.Height > max)
                    {
                        max = childrenControl.Top + childrenControl.Height + 1;
                    }
                }
                return max;
            }
        } // AvailablePositionInsideControl

        public virtual ClipBox ClientArea { get; set; }

        public override Margins ClientMargins
        {
            get => base.ClientMargins;
            set
            {
                // Fix the range
                if (value.Left < 0)
                    value.Left = 0;
                if (value.Right < 0)
                    value.Right = 0;
                if (value.Top < 0)
                    value.Top = 0;
                if (value.Bottom < 0)
                    value.Bottom = 0;

                base.ClientMargins = value;
                if (ClientArea != null)
                {
                    ClientArea.Left = ClientLeft;
                    ClientArea.Top = ClientTop;
                    ClientArea.Width = ClientWidth;
                    ClientArea.Height = ClientHeight;
                }
            }
        } // ClientMargins



        public ClipControl(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            ClientArea = new ClipBox(UserInterfaceManager)
            {
                MinimumWidth = 0,
                MinimumHeight = 0,
                Left = ClientLeft,
                Top = ClientTop,
                Width = ClientWidth,
                Height = ClientHeight
            };

            base.Add(ClientArea);
        } // ClipControl



        public virtual void Add(Control control, bool client)
        {
            if (client)
                ClientArea.Add(control);
            else
                base.Add(control);
        } // Add

        public override void Add(Control control)
        {
            Add(control, true);
        } // Add

        public override void Remove(Control control)
        {
            base.Remove(control);
            ClientArea.Remove(control);
        } // Remove



        public void RemoveControlsFromClientArea()
        {
            // Recursively disposing all children controls.
            // The collection might change from its children, so we check it on count greater than zero.
            if (ClientArea.ChildrenControls != null)
            {
                int childrenControlsCount = ClientArea.ChildrenControls.Count;
                for (int i = childrenControlsCount - 1; i >= 0; i--)
                {
                    ClientArea.ChildrenControls[i].Dispose();
                }
            }
            AdjustMargins();
            Invalidate();
        } // RemoveControlsFromClientArea



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (ClientArea != null)
            {
                ClientArea.Left = ClientLeft;
                ClientArea.Top = ClientTop;
                ClientArea.Width = ClientWidth;
                ClientArea.Height = ClientHeight;
            }
        } // OnResize



        public virtual void AdjustHeightFromChildren()
        {
            Height = AvailablePositionInsideControl + ClientMargins.Top + ClientMargins.Bottom + 6;
        } // AdjustHeightFromChildren



        protected virtual void AdjustMargins()
        {
            // Overrite it!!
        } // AdjustMargins


    } // ClipControl
} // XNAFinalEngine.UserInterface


/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class PanelCollapsible : ClipControl
    {


        private readonly TreeButton treeButton;



        public PanelCollapsible(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CanFocus = false;
            Passive = false;
            BackgroundColor = Color.Transparent;

            // This is the control that manages the collapse functionality.
            treeButton = new TreeButton(UserInterfaceManager);
            treeButton.Anchor = Anchors.Left | Anchors.Right | Anchors.Top;
            Add(treeButton, false);
            treeButton.Width = ClientWidth;
            TextChanged += delegate { treeButton.Text = Text; };
            treeButton.CanFocus = false;

            // The client area is lowered to make place to the previous control.
            Margins m = ClientMargins;
            m.Top += 20;
            ClientMargins = m;

            // If the control is collaped or expanded...
            treeButton.CheckedChanged += delegate
            {
                int differencial;
                if (treeButton.Checked)
                {
                    // Only show the tree button.
                    ClientArea.Visible = false;
                    differencial = -Height + 20;
                    Height = 20;
                }
                else
                {
                    // Show the client are.
                    ClientArea.Visible = true;
                    AdjustHeightFromChildren();
                    differencial = Height - 20;
                }
                if (Parent != null)
                {
                    // Move up or down the controls that are below this control
                    foreach (var childControl in Parent.ChildrenControls)
                    {
                        if (childControl.Top > Top && (childControl.Anchor & Anchors.Top) == Anchors.Top)
                        {
                            childControl.Top += differencial;
                        }
                    }
                }
                // Adjust the scrolling of all parents.
                Invalidate();
            };
            treeButton.Checked = false;
        } // PanelCollapsible



        public override void Add(Control control, bool client)
        {
            base.Add(control, client);
            if (treeButton != null && !treeButton.Checked)
            {
                AdjustHeightFromChildren();
            }
        } // Add

        public override void Remove(Control control)
        {
            base.Remove(control);
            if (treeButton != null && !treeButton.Checked)
            {
                AdjustHeightFromChildren();
            }
        } // Remove



        protected override void DrawControl(Rectangle rect)
        {
            // We only want to render the children.
        } // DrawControl


    } // PanelCollapsible
} // XNAFinalEngine.UserInterface

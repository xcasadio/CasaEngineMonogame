
/*
Copyright (c) 2008-2010, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
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

using TextBox = CasaEngine.Framework.UserInterface.Controls.Text.TextBox;

namespace CasaEngine.Framework.UserInterface.Controls.Windows
{

    public class AssetWindow : Window
    {


        private string _assetName;
        private readonly TextBox _nameTextBox;



        public string AssetType { get; set; }

        public string AssetName
        {
            get => _assetName;
            set
            {
                if (_assetName != value)
                {
                    _assetName = value;
                    OnAssetNameChanged(new EventArgs());
                    _nameTextBox.Text = _assetName;
                }
            }
        } // AssetName

        public override string Text => AssetName + " : " + AssetType; // Text



        public event EventHandler AssetNameChanged;



        public AssetWindow(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            var nameLabel = new Label(UserInterfaceManager)
            {
                Parent = this,
                Text = "Name",
                Left = 10,
                Top = 10,
                Height = 25,
                Alignment = Alignment.BottomCenter,
            };
            _nameTextBox = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Width = ClientWidth - nameLabel.Width - 5,
                Text = Text,
                Left = 60,
                Top = 10,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right
            };
            _nameTextBox.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    AssetName = _nameTextBox.Text;
                }
            };
            _nameTextBox.FocusLost += delegate
            {
                AssetName = _nameTextBox.Text;
            };
        } // AssetWindow



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            AssetNameChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected virtual void OnAssetNameChanged(EventArgs e)
        {
            if (AssetNameChanged != null)
            {
                AssetNameChanged.Invoke(this, e);
            }
        } // OnAssetNameChanged


    } // AssetWindow
} // XNAFinalEngine.UserInterface
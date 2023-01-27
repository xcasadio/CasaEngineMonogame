
#region License
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
#endregion

#region Using directives
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Window with a name text box.
    /// </summary>
    public class AssetWindow : Window
    {

        #region Variables

        private string assetName;
        private readonly TextBox nameTextBox;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the asset type.
        /// </summary>
        public string AssetType { get; set; }

        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string AssetName
        {
            get { return assetName; }
            set
            {
                if (assetName != value)
                {
                    assetName = value;
                    OnAssetNameChanged(new EventArgs());
                    nameTextBox.Text = assetName;
                }
            }
        } // AssetName

        /// <summary>
        /// Window name.
        /// </summary>
        public override string Text
        {
            get { return AssetName + " : " + AssetType; }
        } // Text

        #endregion

        #region Events

        public event EventHandler AssetNameChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Window with a name text box.
        /// </summary>
        public AssetWindow(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
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
            nameTextBox = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Width = ClientWidth - nameLabel.Width - 5,
                Text = Text,
                Left = 60,
                Top = 10,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right
            };
            nameTextBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                    AssetName = nameTextBox.Text;
            };
            nameTextBox.FocusLost += delegate
            {
                AssetName = nameTextBox.Text;
            };
        } // AssetWindow

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            AssetNameChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion
        
        #region Raise Event

        protected virtual void OnAssetNameChanged(EventArgs e)
        {
            if (AssetNameChanged != null)
                AssetNameChanged.Invoke(this, e);
        } // OnAssetNameChanged

        #endregion

    } // AssetWindow
} // XNAFinalEngine.UserInterface

/*
Copyright (c) 2008-2010, Laboratorio de Investigaci�n y Desarrollo en Visualizaci�n y Computaci�n Gr�fica - 
                         Departamento de Ciencias e Ingenier�a de la Computaci�n - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

�	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

�	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

�	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, Jos� Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class AssetSelector : Control
    {


        // Controls.
        private readonly ComboBox comboBox;
        private readonly Button buttonAdd;
        private readonly Button buttonEdit;



        public bool ListBoxVisible => comboBox.ListBoxVisible;

        public virtual List<object> Items => comboBox.Items; // Items

        public int MaxItemsShow
        {
            get => comboBox.MaxItemsShow;
            set => comboBox.MaxItemsShow = value;
        } // MaxItems

        public int ItemIndex
        {
            get => comboBox.ItemIndex;
            set => comboBox.ItemIndex = value;
        } // ItemIndex

        public bool EditButtonEnabled { get; set; }



        public event EventHandler AssetAdded;
        public event EventHandler AssetEdited;
        public event EventHandler MaxItemsChanged;
        public event EventHandler ItemIndexChanged;



        public AssetSelector(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Anchor = Anchors.Left | Anchors.Right | Anchors.Top;
            Width = 420;
            Height = 30;
            CanFocus = false;
            Passive = true;
            EditButtonEnabled = true;
            var label = new Label(UserInterfaceManager)
            {
                Parent = this,
                Width = 150,
                Height = 25,
            };
            TextChanged += delegate { label.Text = Text; };

            buttonAdd = new Button(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top,
                Left = 140,
                Width = 15,
                Height = 20,
                ToolTip = { Text = "Add new asset" },
                Text = "+"
            };
            buttonAdd.Click += delegate { OnAssetAdded(new EventArgs()); };
            buttonEdit = new Button(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top,
                Left = 160,
                Width = 15,
                Height = 20,
                ToolTip = { Text = "Edit current asset" },
                Text = "E",

            };
            buttonEdit.Click += delegate { OnAssetEdited(new EventArgs()); };

            comboBox = new ComboBox(UserInterfaceManager)
            {
                Parent = this,
                Left = 180,
                Height = 20,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                MaxItemsShow = 25,
                Width = 235,
            };

            buttonEdit.Enabled = comboBox.ItemIndex > 0;

            comboBox.MaxItemsChanged += delegate { OnMaxItemsChanged(new EventArgs()); };
            comboBox.ItemIndexChanged += delegate { OnItemIndexChanged(new EventArgs()); };

        } // SliderNumeric



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            AssetAdded = null;
            AssetEdited = null;
            MaxItemsChanged = null;
            ItemIndexChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            buttonEdit.Enabled = EditButtonEnabled && Enabled;
            // Only the children will be rendered.
        } // DrawControl



        protected virtual void OnAssetAdded(EventArgs e)
        {
            if (AssetAdded != null)
                AssetAdded.Invoke(this, e);
        } // OnAssetAdded

        protected virtual void OnAssetEdited(EventArgs e)
        {
            if (AssetEdited != null)
                AssetEdited.Invoke(this, e);
        } // OnAssetEdited

        protected virtual void OnMaxItemsChanged(EventArgs e)
        {
            if (MaxItemsChanged != null)
                MaxItemsChanged.Invoke(this, e);
        } // OnMaxItemsChanged

        protected virtual void OnItemIndexChanged(EventArgs e)
        {
            if (ItemIndexChanged != null)
                ItemIndexChanged.Invoke(this, e);
        } // OnItemIndexChanged


    } // AssetSelector
} // XNAFinalEngine.UserInterface
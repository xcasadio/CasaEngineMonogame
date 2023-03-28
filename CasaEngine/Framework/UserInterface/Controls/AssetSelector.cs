
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

using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.Framework.UserInterface.Controls;

public class AssetSelector : Control
{


    // Controls.
    private readonly ComboBox _comboBox;
    private readonly Button _buttonAdd;
    private readonly Button _buttonEdit;



    public bool ListBoxVisible => _comboBox.ListBoxVisible;

    public virtual List<object> Items => _comboBox.Items; // Items

    public int MaxItemsShow
    {
        get => _comboBox.MaxItemsShow;
        set => _comboBox.MaxItemsShow = value;
    } // MaxItems

    public int ItemIndex
    {
        get => _comboBox.ItemIndex;
        set => _comboBox.ItemIndex = value;
    } // ItemIndex

    public bool EditButtonEnabled { get; set; }



    public event EventHandler AssetAdded;
    public event EventHandler AssetEdited;
    public event EventHandler MaxItemsChanged;
    public event EventHandler ItemIndexChanged;



    public AssetSelector(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
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

        _buttonAdd = new Button(UserInterfaceManager)
        {
            Parent = this,
            Anchor = Anchors.Left | Anchors.Top,
            Left = 140,
            Width = 15,
            Height = 20,
            ToolTip = { Text = "Add new asset" },
            Text = "+"
        };
        _buttonAdd.Click += delegate { OnAssetAdded(new EventArgs()); };
        _buttonEdit = new Button(UserInterfaceManager)
        {
            Parent = this,
            Anchor = Anchors.Left | Anchors.Top,
            Left = 160,
            Width = 15,
            Height = 20,
            ToolTip = { Text = "Edit current asset" },
            Text = "E",

        };
        _buttonEdit.Click += delegate { OnAssetEdited(new EventArgs()); };

        _comboBox = new ComboBox(UserInterfaceManager)
        {
            Parent = this,
            Left = 180,
            Height = 20,
            Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
            MaxItemsShow = 25,
            Width = 235,
        };

        _buttonEdit.Enabled = _comboBox.ItemIndex > 0;

        _comboBox.MaxItemsChanged += delegate { OnMaxItemsChanged(new EventArgs()); };
        _comboBox.ItemIndexChanged += delegate { OnItemIndexChanged(new EventArgs()); };

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
        _buttonEdit.Enabled = EditButtonEnabled && Enabled;
        // Only the children will be rendered.
    } // DrawControl



    protected virtual void OnAssetAdded(EventArgs e)
    {
        if (AssetAdded != null)
        {
            AssetAdded.Invoke(this, e);
        }
    } // OnAssetAdded

    protected virtual void OnAssetEdited(EventArgs e)
    {
        if (AssetEdited != null)
        {
            AssetEdited.Invoke(this, e);
        }
    } // OnAssetEdited

    protected virtual void OnMaxItemsChanged(EventArgs e)
    {
        if (MaxItemsChanged != null)
        {
            MaxItemsChanged.Invoke(this, e);
        }
    } // OnMaxItemsChanged

    protected virtual void OnItemIndexChanged(EventArgs e)
    {
        if (ItemIndexChanged != null)
        {
            ItemIndexChanged.Invoke(this, e);
        }
    } // OnItemIndexChanged


} // AssetSelector
  // XNAFinalEngine.UserInterface
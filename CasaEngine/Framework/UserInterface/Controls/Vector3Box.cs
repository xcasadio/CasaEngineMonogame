
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

using Microsoft.Xna.Framework;
using TextBox = CasaEngine.Framework.UserInterface.Controls.Text.TextBox;

namespace CasaEngine.Framework.UserInterface.Controls;

public class Vector3Box : Control
{


    // Controls
    private readonly TextBox _xTextBox, _yTextBox, _zTextBox;

    private Vector3 _value;



    public virtual Vector3 Value
    {
        get => _value;
        set
        {
            if (value != Value)
            {
                _value = value;
                OnValueChanged(new EventArgs());
            }
        }
    } // Value



    public event EventHandler ValueChanged;



    protected override void DisposeManagedResources()
    {
        // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
        ValueChanged = null;
        base.DisposeManagedResources();
    } // DisposeManagedResources



    public Vector3Box(UserInterfaceManager userInterfaceManager)
        : base(userInterfaceManager)
    {
        Anchor = Anchors.Left | Anchors.Right | Anchors.Top;
        Width = 420;
        Height = 25;
        CanFocus = false;
        Passive = true;
        var xLabel = new Label(UserInterfaceManager)
        {
            Parent = this,
            Width = 15,
            Height = 25,
            Text = "X"
        };
        _xTextBox = new TextBox(UserInterfaceManager)
        {
            Parent = this,
            Width = 100,
            Left = xLabel.Width,
            Text = "0",
        };
        var yLabel = new Label(UserInterfaceManager)
        {
            Parent = this,
            Left = _xTextBox.Left + _xTextBox.Width + 8,
            Width = 15,
            Height = 25,
            Text = "Y"
        };
        _yTextBox = new TextBox(UserInterfaceManager)
        {
            Parent = this,
            Width = 100,
            Left = yLabel.Left + yLabel.Width,
            Text = "0",
        };
        var zLabel = new Label(UserInterfaceManager)
        {
            Parent = this,
            Left = _yTextBox.Left + _yTextBox.Width + 8,
            Width = 15,
            Height = 25,
            Text = "Z"
        };
        _zTextBox = new TextBox(UserInterfaceManager)
        {
            Parent = this,
            Left = zLabel.Left + zLabel.Width,
            Width = 100,
            Text = "0",
        };


        KeyEventHandler keyHandler = delegate (object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Enter)
            {
                try
                {
                    Value = new Vector3((float)double.Parse(_xTextBox.Text), (float)double.Parse(_yTextBox.Text), (float)double.Parse(_zTextBox.Text));
                }
                catch // If not numeric
                {
                    _xTextBox.Text = _value.X.ToString();
                    _yTextBox.Text = _value.Y.ToString();
                    _zTextBox.Text = _value.Z.ToString();
                }
            }
        };
        _xTextBox.KeyDown += keyHandler;
        _yTextBox.KeyDown += keyHandler;
        _zTextBox.KeyDown += keyHandler;
        // For tabs and other not so common things.
        EventHandler focusHandler = delegate
        {
            try
            {
                Value = new Vector3((float)double.Parse(_xTextBox.Text), (float)double.Parse(_yTextBox.Text), (float)double.Parse(_zTextBox.Text));
            }
            catch // If not numeric
            {
                _xTextBox.Text = _value.X.ToString();
                _yTextBox.Text = _value.Y.ToString();
                _zTextBox.Text = _value.Z.ToString();
            }
        };
        _xTextBox.FocusLost += focusHandler;
        _yTextBox.FocusLost += focusHandler;
        _zTextBox.FocusLost += focusHandler;

        ValueChanged += delegate
        {
            _xTextBox.Text = _value.X.ToString();
            _yTextBox.Text = _value.Y.ToString();
            _zTextBox.Text = _value.Z.ToString();
        };


    } // Vector3Box



    protected override void DrawControl(Rectangle rect)
    {
        // Only the children will be rendered.
    } // DrawControl



    protected virtual void OnValueChanged(EventArgs e)
    {
        if (ValueChanged != null)
        {
            ValueChanged.Invoke(this, e);
        }
    } // OnValueChanged


} // Vector3Box
  // XNAFinalEngine.UserInterface
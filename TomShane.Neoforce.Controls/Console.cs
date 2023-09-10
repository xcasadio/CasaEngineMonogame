using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace TomShane.Neoforce.Controls;

public struct ConsoleMessage
{
    public readonly string Text;
    public readonly byte Channel;
    public DateTime Time;
    public readonly string Sender;

    public ConsoleMessage(string sender, string text, byte channel)
    {
        Text = text;
        Channel = channel;
        Time = DateTime.Now;
        Sender = sender;
    }
}

public class ChannelList : EventedList<ConsoleChannel>
{

    public ConsoleChannel this[string name]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Name.ToLower() == name.ToLower())
                {
                    return s;
                }
            }
            return default(ConsoleChannel);
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Name.ToLower() == name.ToLower())
                {
                    this[i] = value;
                }
            }
        }
    }

    public ConsoleChannel this[byte index]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Index == index)
                {
                    return s;
                }
            }
            return default(ConsoleChannel);
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Index == index)
                {
                    this[i] = value;
                }
            }
        }
    }

}

public class ConsoleChannel
{
    private string _name;
    private byte _index;
    private Color _color;

    public ConsoleChannel(byte index, string name, Color color)
    {
        _name = name;
        _index = index;
        _color = color;
    }

    public virtual byte Index
    {
        get => _index;
        set => _index = value;
    }

    public virtual Color Color
    {
        get => _color;
        set => _color = value;
    }

    public virtual string Name
    {
        get => _name;
        set => _name = value;
    }
}

[Flags]
public enum ConsoleMessageFormats
{
    None = 0x00,
    ChannelName = 0x01,
    TimeStamp = 0x02,
    Sender = 0x03,
    All = Sender | ChannelName | TimeStamp
}

public class Console : Container
{

    private TextBox _txtMain;
    private ComboBox _cmbMain;
    private EventedList<ConsoleMessage> _buffer = new();
    private ChannelList _channels = new();
    private List<byte> _filter = new();
    private ConsoleMessageFormats _messageFormat = ConsoleMessageFormats.None;
    private bool _channelsVisible = true;
    private bool _textBoxVisible = true;

    public string Sender { get; set; }

    public virtual EventedList<ConsoleMessage> MessageBuffer
    {
        get => _buffer;
        set
        {
            _buffer.ItemAdded -= new EventHandler(buffer_ItemAdded);
            _buffer = value;
            _buffer.ItemAdded += new EventHandler(buffer_ItemAdded);
        }
    }

    public virtual ChannelList Channels
    {
        get => _channels;
        set
        {
            _channels.ItemAdded -= new EventHandler(channels_ItemAdded);
            _channels = value;
            _channels.ItemAdded += new EventHandler(channels_ItemAdded);
            channels_ItemAdded(null, null);
        }
    }

    public virtual List<byte> ChannelFilter
    {
        get => _filter;
        set => _filter = value;
    }

    public virtual byte SelectedChannel
    {
        set => _cmbMain.Text = _channels[value].Name;
        get => _channels[_cmbMain.Text].Index;
    }

    public virtual ConsoleMessageFormats MessageFormat
    {
        get => _messageFormat;
        set => _messageFormat = value;
    }

    public virtual bool ChannelsVisible
    {
        get => _channelsVisible;
        set
        {
            _cmbMain.Visible = _channelsVisible = value;
            if (value && !_textBoxVisible)
            {
                TextBoxVisible = false;
            }

            PositionControls();
        }
    }

    public virtual bool TextBoxVisible
    {
        get => _textBoxVisible;
        set
        {
            _txtMain.Visible = _textBoxVisible = value;
            _txtMain.Focused = true;
            if (!value && _channelsVisible)
            {
                ChannelsVisible = false;
            }

            PositionControls();
        }
    }

    public event ConsoleMessageEventHandler MessageSent;

    public Console(Manager manager)
        : base(manager)
    {
        Width = 320;
        Height = 160;
        MinimumHeight = 64;
        MinimumWidth = 64;
        CanFocus = false;
        Resizable = false;
        Movable = false;

        _cmbMain = new ComboBox(manager);
        _cmbMain.Init();
        _cmbMain.Top = Height - _cmbMain.Height;
        _cmbMain.Left = 0;
        _cmbMain.Width = 128;
        _cmbMain.Anchor = Anchors.Left | Anchors.Bottom;
        _cmbMain.Detached = false;
        _cmbMain.DrawSelection = false;
        _cmbMain.Visible = _channelsVisible;
        Add(_cmbMain, false);

        _txtMain = new TextBox(manager);
        _txtMain.Init();
        _txtMain.Top = Height - _txtMain.Height;
        _txtMain.Left = _cmbMain.Width + 1;
        _txtMain.Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right;
        _txtMain.Detached = false;
        _txtMain.Visible = _textBoxVisible;
        _txtMain.KeyDown += new KeyEventHandler(txtMain_KeyDown);
        _txtMain.GamePadDown += new GamePadEventHandler(txtMain_GamePadDown);
        _txtMain.FocusGained += new EventHandler(txtMain_FocusGained);
        Add(_txtMain, false);

        VerticalScrollBar.Top = 2;
        VerticalScrollBar.Left = Width - 18;
        VerticalScrollBar.Range = 1;
        VerticalScrollBar.PageSize = 1;
        VerticalScrollBar.ValueChanged += new EventHandler(VerticalScrollBar_ValueChanged);
        VerticalScrollBar.Visible = true;

        ClientArea.Draw += new DrawEventHandler(ClientArea_Draw);

        _buffer.ItemAdded += new EventHandler(buffer_ItemAdded);
        _channels.ItemAdded += new EventHandler(channels_ItemAdded);
        _channels.ItemRemoved += new EventHandler(channels_ItemRemoved);

        PositionControls();
    }

    private void PositionControls()
    {
        if (_txtMain != null)
        {
            _txtMain.Left = _channelsVisible ? _cmbMain.Width + 1 : 0;
            _txtMain.Width = _channelsVisible ? Width - _cmbMain.Width - 1 : Width;

            if (_textBoxVisible)
            {
                ClientMargins = new Margins(Skin.ClientMargins.Left, Skin.ClientMargins.Top + 4, VerticalScrollBar.Width + 6, _txtMain.Height + 4);
                VerticalScrollBar.Height = Height - _txtMain.Height - 5;
            }
            else
            {
                ClientMargins = new Margins(Skin.ClientMargins.Left, Skin.ClientMargins.Top + 4, VerticalScrollBar.Width + 6, 2);
                VerticalScrollBar.Height = Height - 4;
            }
            Invalidate();
        }
    }

    public override void Init()
    {
        base.Init();
    }

    protected internal override void InitSkin()
    {
        base.InitSkin();
        Skin = new SkinControl(Manager.Skin.Controls["Console"]);

        PositionControls();
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    void ClientArea_Draw(object sender, DrawEventArgs e)
    {
        var font = Skin.Layers[0].Text.Font.Resource;
        var r = new Rectangle(e.Rectangle.Left, e.Rectangle.Top, e.Rectangle.Width, e.Rectangle.Height);
        var pos = 0;

        if (_buffer.Count > 0)
        {
            var b = GetFilteredBuffer(_filter);
            var c = b.Count;
            var s = VerticalScrollBar.Value + VerticalScrollBar.PageSize;
            var f = s - VerticalScrollBar.PageSize;

            if (b.Count > 0)
            {
                for (var i = s - 1; i >= f; i--)
                {
                    {
                        var x = 4;
                        var y = r.Bottom - (pos + 1) * (font.LineHeight + 0);

                        var msg = b[i].Text;
                        var pre = "";
                        var ch = _channels[b[i].Channel] as ConsoleChannel;

                        if ((_messageFormat & ConsoleMessageFormats.ChannelName) == ConsoleMessageFormats.ChannelName)
                        {
                            pre += $"[{_channels[b[i].Channel].Name}]";
                        }
                        if ((_messageFormat & ConsoleMessageFormats.Sender) == ConsoleMessageFormats.Sender)
                        {
                            pre += $"[{b[i].Sender}]";
                        }
                        if ((_messageFormat & ConsoleMessageFormats.TimeStamp) == ConsoleMessageFormats.TimeStamp)
                        {
                            pre = $"[{b[i].Time.ToLongTimeString()}]" + pre;
                        }

                        if (pre != "")
                        {
                            msg = pre + ": " + msg;
                        }

                        e.Renderer.DrawString(font,
                            msg,
                            x, y,
                            ch.Color);
                        pos += 1;
                    }
                }
            }
        }
    }

    protected override void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
    {
        var h = _txtMain.Visible ? _txtMain.Height + 1 : 0;
        var r = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - h);
        base.DrawControl(renderer, r, gameTime);
    }

    void txtMain_FocusGained(object sender, EventArgs e)
    {
        var ch = _channels[_cmbMain.Text];
        if (ch != null)
        {
            _txtMain.TextColor = ch.Color;
        }
    }

    void txtMain_KeyDown(object sender, KeyEventArgs e)
    {
        SendMessage(e);
    }

    void txtMain_GamePadDown(object sender, GamePadEventArgs e)
    {
        SendMessage(e);
    }

    private void SendMessage(EventArgs x)
    {
        if (Manager.UseGuide)
        {
            return;
        }

        var k = new KeyEventArgs();
        var g = new GamePadEventArgs(PlayerIndex.One);

        if (x is KeyEventArgs eventArgs)
        {
            k = eventArgs;
        }
        else if (x is GamePadEventArgs args)
        {
            g = args;
        }

        var ch = _channels[_cmbMain.Text];
        if (ch != null)
        {
            _txtMain.TextColor = ch.Color;

            var message = _txtMain.Text;
            if ((k.Key == Microsoft.Xna.Framework.Input.Keys.Enter || g.Button == GamePadActions.Press) && !string.IsNullOrEmpty(message))
            {
                x.Handled = true;

                var me = new ConsoleMessageEventArgs(new ConsoleMessage(Sender, message, ch.Index));
                OnMessageSent(me);

                _buffer.Add(new ConsoleMessage(Sender, me.Message.Text, me.Message.Channel));

                _txtMain.Text = "";
                ClientArea.Invalidate();

                CalcScrolling();
            }
        }
    }

    protected virtual void OnMessageSent(ConsoleMessageEventArgs e)
    {
        MessageSent?.Invoke(this, e);
    }

    void channels_ItemAdded(object sender, EventArgs e)
    {
        _cmbMain.Items.Clear();
        for (var i = 0; i < _channels.Count; i++)
        {
            _cmbMain.Items.Add((_channels[i] as ConsoleChannel).Name);
        }
    }

    void channels_ItemRemoved(object sender, EventArgs e)
    {
        _cmbMain.Items.Clear();
        for (var i = 0; i < _channels.Count; i++)
        {
            _cmbMain.Items.Add((_channels[i] as ConsoleChannel).Name);
        }
    }

    void buffer_ItemAdded(object sender, EventArgs e)
    {
        CalcScrolling();
        ClientArea.Invalidate();
    }

    private void CalcScrolling()
    {
        if (VerticalScrollBar != null)
        {
            var line = Skin.Layers[0].Text.Font.Resource.LineHeight;
            var c = GetFilteredBuffer(_filter).Count;
            var p = (int)Math.Ceiling(ClientArea.ClientHeight / (float)line);

            VerticalScrollBar.Range = c == 0 ? 1 : c;
            VerticalScrollBar.PageSize = c == 0 ? 1 : p;
            VerticalScrollBar.Value = VerticalScrollBar.Range;
        }
    }

    void VerticalScrollBar_ValueChanged(object sender, EventArgs e)
    {
        ClientArea.Invalidate();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        CalcScrolling();
        base.OnResize(e);
    }

    private EventedList<ConsoleMessage> GetFilteredBuffer(List<byte> filter)
    {
        var ret = new EventedList<ConsoleMessage>();

        if (filter.Count > 0)
        {
            for (var i = 0; i < _buffer.Count; i++)
            {
                if (filter.Contains(_buffer[i].Channel))
                {
                    ret.Add(_buffer[i]);
                }
            }
            return ret;
        }

        return _buffer;
    }

}
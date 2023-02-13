
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.UserInterface.Controls.Auxiliary;
using ScrollBar = CasaEngine.UserInterface.Controls.Auxiliary.ScrollBar;

namespace CasaEngine.UserInterface.Controls.Text
{


    public struct ConsoleMessage
    {
        public string Text;
        public byte Channel;
        public DateTime Time;

        public ConsoleMessage(string text, byte channel)
        {
            Text = text;
            Channel = channel;
            Time = DateTime.Now;
        }
    } // ConsoleMessage



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
        } // ConsoleChannel

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
        } // ConsoleChannel
    } // ChannelList



    public class ConsoleChannel
    {
        public ConsoleChannel(byte index, string name, Color color)
        {
            Name = name;
            Index = index;
            Color = color;
        } // ConsoleChannel

        public virtual byte Index { get; set; }

        public virtual Color Color { get; set; }

        public virtual string Name { get; set; }
    } // ConsoleChannel



    [Flags]
    public enum ConsoleMessageFormats
    {
        None = 0x00,
        ChannelName = 0x01,
        TimeStamp = 0x02,
        All = ChannelName | TimeStamp
    } // ConsoleMessageFormats


    public class Console : Container
    {


        private readonly TextBox _textMain;
        private readonly ComboBox _cmbMain;
        private readonly ScrollBar _sbVert;
        private EventedList<ConsoleMessage> _buffer = new();
        private ChannelList _channels = new();
        private List<byte> _filter = new();
        private ConsoleMessageFormats _messageFormat = ConsoleMessageFormats.None;
        private bool _channelsVisible = true;
        private bool _textBoxVisible = true;



        public virtual EventedList<ConsoleMessage> MessageBuffer
        {
            get => _buffer;
            set
            {
                _buffer.ItemAdded -= Buffer_ItemAdded;
                _buffer = value;
                _buffer.ItemAdded += Buffer_ItemAdded;
            }
        } // MessageBuffer

        public virtual ChannelList Channels
        {
            get => _channels;
            set
            {
                _channels.ItemAdded -= Channels_ItemAdded;
                _channels = value;
                _channels.ItemAdded += Channels_ItemAdded;
                Channels_ItemAdded(null, null);
            }
        } // Channels

        public virtual List<byte> ChannelFilter
        {
            get => _filter;
            set => _filter = value;
        } // ChannelFilter

        public virtual byte SelectedChannel
        {
            set => _cmbMain.Text = _channels[value].Name;
            get => _channels[_cmbMain.Text].Index;
        } // SelectedChannel

        public virtual ConsoleMessageFormats MessageFormat
        {
            get => _messageFormat;
            set => _messageFormat = value;
        } // MessageFormat

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
        } // ChannelsVisible

        public virtual bool TextBoxVisible
        {
            get => _textBoxVisible;
            set
            {
                _textMain.Visible = _textBoxVisible = value;
                if (!value && _channelsVisible)
                {
                    ChannelsVisible = false;
                }

                PositionControls();
            }
        } // TextBoxVisible



        public event ConsoleMessageEventHandler MessageSent;



        public Console(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Width = 320;
            Height = 160;
            MinimumHeight = 64;
            MinimumWidth = 64;
            CanFocus = false;

            Resizable = false;
            Movable = false;

            _cmbMain = new ComboBox(UserInterfaceManager)
            {
                Left = 0,
                Width = 128,
                Anchor = Anchors.Left | Anchors.Bottom,
                Detached = false,
                DrawSelection = false,
                Visible = _channelsVisible
            };
            _cmbMain.Top = Height - _cmbMain.Height;
            Add(_cmbMain, false);

            _textMain = new TextBox(UserInterfaceManager)
            {
                Left = _cmbMain.Width + 1,
                Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right,
                Detached = false,
                Visible = _textBoxVisible
            };
            _textMain.Top = Height - _textMain.Height;
            _textMain.KeyDown += TextMain_KeyDown;
            _textMain.FocusGained += TextMain_FocusGained;
            Add(_textMain, false);

            _sbVert = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Top = 2,
                Left = Width - 18,
                Anchor = Anchors.Right | Anchors.Top | Anchors.Bottom,
                Range = 1,
                PageSize = 1,
                Value = 0
            };
            _sbVert.ValueChanged += ScrollBarVertical_ValueChanged;
            Add(_sbVert, false);

            ClientArea.Draw += ClientArea_Draw;

            _buffer.ItemAdded += Buffer_ItemAdded;
            _channels.ItemAdded += Channels_ItemAdded;
            _channels.ItemRemoved += Channels_ItemRemoved;

            PositionControls();
        } // Console



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Console"]);
            PositionControls();
        } // InitSkin



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            MessageSent = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        private void ClientArea_Draw(object sender, DrawEventArgs e)
        {
            var font = SkinInformation.Layers[0].Text.Font.Font;
            var r = new Rectangle(e.Rectangle.Left, e.Rectangle.Top, e.Rectangle.Width, e.Rectangle.Height);
            var pos = 0;

            if (_buffer.Count > 0)
            {
                var b = GetFilteredBuffer(_filter);
                var s = (_sbVert.Value + _sbVert.PageSize);
                var f = s - _sbVert.PageSize;

                if (b.Count > 0)
                {
                    for (var i = s - 1; i >= f; i--)
                    {
                        {
                            var y = r.Bottom - (pos + 1) * (font.LineSpacing + 0);

                            var msg = b[i].Text;
                            var pre = "";
                            var ch = _channels[b[i].Channel];

                            if ((_messageFormat & ConsoleMessageFormats.ChannelName) == ConsoleMessageFormats.ChannelName)
                            {
                                pre += string.Format("[{0}]", _channels[b[i].Channel].Name);
                            }
                            if ((_messageFormat & ConsoleMessageFormats.TimeStamp) == ConsoleMessageFormats.TimeStamp)
                            {
                                pre = string.Format("[{0}]", b[i].Time.ToLongTimeString()) + pre;
                            }

                            if (pre != "")
                            {
                                msg = pre + ": " + msg;
                            }

                            UserInterfaceManager.Renderer.DrawString(font, msg, 4, y, ch.Color);
                            pos += 1;
                        }
                    }
                }
            }
        } // ClientArea_Draw

        protected override void DrawControl(Rectangle rect)
        {
            var h = _textMain.Visible ? (_textMain.Height + 1) : 0;
            var r = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - h);
            base.DrawControl(r);
        } // DrawControl



        private void PositionControls()
        {
            if (_textMain != null)
            {
                _textMain.Left = _channelsVisible ? _cmbMain.Width + 1 : 0;
                _textMain.Width = _channelsVisible ? Width - _cmbMain.Width - 1 : Width;

                if (_textBoxVisible)
                {
                    ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.ClientMargins.Top + 4, _sbVert.Width + 6, _textMain.Height + 4);
                    _sbVert.Height = Height - _textMain.Height - 5;
                }
                else
                {
                    ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.ClientMargins.Top + 4, _sbVert.Width + 6, 2);
                    _sbVert.Height = Height - 4;
                }
                Invalidate();
            }
        } // PositionControls



        private void TextMain_FocusGained(object sender, EventArgs e)
        {
            var ch = _channels[_cmbMain.Text];
            if (ch != null)
            {
                _textMain.TextColor = ch.Color;
            }
        } // TextMain_FocusGained

        private void TextMain_KeyDown(object sender, KeyEventArgs e)
        {
            SendMessage(e);
        } // TextMain_KeyDown

        private void SendMessage(EventArgs x)
        {
            var k = new KeyEventArgs();

            if (x is KeyEventArgs)
            {
                k = x as KeyEventArgs;
            }

            var ch = _channels[_cmbMain.Text];
            if (ch != null)
            {
                _textMain.TextColor = ch.Color;

                var message = _textMain.Text;
                if ((k.Key == Keys.Enter) && !string.IsNullOrEmpty(message))
                {
                    x.Handled = true;

                    var me = new ConsoleMessageEventArgs(new ConsoleMessage(message, ch.Index));
                    OnMessageSent(me);

                    _buffer.Add(new ConsoleMessage(me.Message.Text, me.Message.Channel));

                    _textMain.Text = "";
                    ClientArea.Invalidate();

                    CalcScrolling();
                }
            }
        } // SendMessage

        private void OnMessageSent(ConsoleMessageEventArgs e)
        {
            if (MessageSent != null)
            {
                MessageSent.Invoke(this, e);
            }
        } // OnMessageSent

        private void Channels_ItemAdded(object sender, EventArgs e)
        {
            _cmbMain.Items.Clear();
            foreach (var t in _channels)
            {
                _cmbMain.Items.Add(t.Name);
            }
        } // Channels_ItemAdded

        private void Channels_ItemRemoved(object sender, EventArgs e)
        {
            _cmbMain.Items.Clear();
            foreach (var t in _channels)
            {
                _cmbMain.Items.Add(t.Name);
            }
        } // Channels_ItemRemoved

        private void Buffer_ItemAdded(object sender, EventArgs e)
        {
            CalcScrolling();
            ClientArea.Invalidate();
        } // Buffer_ItemAdded

        private void CalcScrolling()
        {
            if (_sbVert != null)
            {
                var line = SkinInformation.Layers[0].Text.Font.Font.LineSpacing;
                var c = GetFilteredBuffer(_filter).Count;
                var p = (int)Math.Ceiling(ClientArea.ClientHeight / (float)line);

                _sbVert.Range = c == 0 ? 1 : c;
                _sbVert.PageSize = c == 0 ? 1 : p;
                _sbVert.Value = _sbVert.Range;
            }
        } // CalcScrolling

        private void ScrollBarVertical_ValueChanged(object sender, EventArgs e)
        {
            ClientArea.Invalidate();
        } // ScrollBarVertical_ValueChanged

        protected override void OnResize(ResizeEventArgs e)
        {
            CalcScrolling();
            base.OnResize(e);
        } // OnResize

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
        } // GetFilteredBuffer


    } // Console
} // XNAFinalEngine.UserInterface
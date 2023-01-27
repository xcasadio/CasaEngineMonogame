
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Asset.Fonts;
#endregion

namespace XNAFinalEngine.UserInterface
{

    #region Console Message

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

    #endregion

    #region Channel List

    public class ChannelList : EventedList<ConsoleChannel>
    {

        public ConsoleChannel this[string name]
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    ConsoleChannel s = this[i];
                    if (s.Name.ToLower() == name.ToLower())
                    {
                        return s;
                    }
                }
                return default(ConsoleChannel);
            }
            set
            {
                for (int i = 0; i < Count; i++)
                {
                    ConsoleChannel s = this[i];
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
                for (int i = 0; i < Count; i++)
                {
                    ConsoleChannel s = this[i];
                    if (s.Index == index)
                    {
                        return s;
                    }
                }
                return default(ConsoleChannel);
            }
            set
            {
                for (int i = 0; i < Count; i++)
                {
                    ConsoleChannel s = this[i];
                    if (s.Index == index)
                    {
                        this[i] = value;
                    }
                }
            }
        } // ConsoleChannel
    } // ChannelList

    #endregion

    #region Console Channel

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

    #endregion

    #region Console Message Formats

    [Flags]
    public enum ConsoleMessageFormats
    {
        None = 0x00,
        ChannelName = 0x01,
        TimeStamp = 0x02,
        All = ChannelName | TimeStamp
    } // ConsoleMessageFormats

    #endregion

    /// <summary>
    /// Console.
    /// </summary>
    public class Console : Container
    {

        #region Variables

        private readonly TextBox textMain;
        private readonly ComboBox cmbMain;
        private readonly ScrollBar sbVert;
        private EventedList<ConsoleMessage> buffer = new EventedList<ConsoleMessage>();
        private ChannelList channels = new ChannelList();
        private List<byte> filter = new List<byte>();
        private ConsoleMessageFormats messageFormat = ConsoleMessageFormats.None;
        private bool channelsVisible = true;
        private bool textBoxVisible = true;

        #endregion

        #region Properties

        public virtual EventedList<ConsoleMessage> MessageBuffer
        {
            get { return buffer; }
            set
            {
                buffer.ItemAdded -= Buffer_ItemAdded;
                buffer = value;
                buffer.ItemAdded += Buffer_ItemAdded;
            }
        } // MessageBuffer

        public virtual ChannelList Channels
        {
            get { return channels; }
            set
            {
                channels.ItemAdded -= Channels_ItemAdded;
                channels = value;
                channels.ItemAdded += Channels_ItemAdded;
                Channels_ItemAdded(null, null);
            }
        } // Channels

        public virtual List<byte> ChannelFilter
        {
            get { return filter; }
            set { filter = value; }
        } // ChannelFilter

        public virtual byte SelectedChannel
        {
            set { cmbMain.Text = channels[value].Name; }
            get { return channels[cmbMain.Text].Index; }
        } // SelectedChannel

        public virtual ConsoleMessageFormats MessageFormat
        {
            get { return messageFormat; }
            set { messageFormat = value; }
        } // MessageFormat

        public virtual bool ChannelsVisible
        {
            get { return channelsVisible; }
            set
            {
                cmbMain.Visible = channelsVisible = value;
                if (value && !textBoxVisible) TextBoxVisible = false;
                PositionControls();
            }
        } // ChannelsVisible

        public virtual bool TextBoxVisible
        {
            get { return textBoxVisible; }
            set
            {
                textMain.Visible = textBoxVisible = value;
                if (!value && channelsVisible) ChannelsVisible = false;
                PositionControls();
            }
        } // TextBoxVisible

        #endregion

        #region Events

        public event ConsoleMessageEventHandler MessageSent;

        #endregion

        #region Constructor

        /// <summary>
        /// Console.
        /// </summary>
        public Console(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Width = 320;
            Height = 160;
            MinimumHeight = 64;
            MinimumWidth = 64;
            CanFocus = false;

            Resizable = false;
            Movable = false;

            cmbMain = new ComboBox(UserInterfaceManager)
            {
                Left = 0,
                Width = 128,
                Anchor = Anchors.Left | Anchors.Bottom,
                Detached = false,
                DrawSelection = false,
                Visible = channelsVisible
            };
            cmbMain.Top = Height - cmbMain.Height;
            Add(cmbMain, false);

            textMain = new TextBox(UserInterfaceManager)
                          {
                              Left = cmbMain.Width + 1,
                              Anchor = Anchors.Left | Anchors.Bottom | Anchors.Right,
                              Detached = false,
                              Visible = textBoxVisible
                          };
            textMain.Top = Height - textMain.Height;
            textMain.KeyDown += TextMain_KeyDown;
            textMain.FocusGained += TextMain_FocusGained;
            Add(textMain, false);

            sbVert = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Top = 2,
                Left = Width - 18,
                Anchor = Anchors.Right | Anchors.Top | Anchors.Bottom,
                Range = 1,
                PageSize = 1,
                Value = 0
            };
            sbVert.ValueChanged += ScrollBarVertical_ValueChanged;
            Add(sbVert, false);

            ClientArea.Draw += ClientArea_Draw;

            buffer.ItemAdded += Buffer_ItemAdded;
            channels.ItemAdded += Channels_ItemAdded;
            channels.ItemRemoved += Channels_ItemRemoved;

            PositionControls();
        } // Console

        #endregion

        #region Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Console"]);
            PositionControls();
        } // InitSkin

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            MessageSent = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion
        
        #region Draw

        private void ClientArea_Draw(object sender, DrawEventArgs e)
        {
            Font font = SkinInformation.Layers[0].Text.Font.Font;
            Rectangle r = new Rectangle(e.Rectangle.Left, e.Rectangle.Top, e.Rectangle.Width, e.Rectangle.Height);
            int pos = 0;

            if (buffer.Count > 0)
            {
                EventedList<ConsoleMessage> b = GetFilteredBuffer(filter);
                int s = (sbVert.Value + sbVert.PageSize);
                int f = s - sbVert.PageSize;

                if (b.Count > 0)
                {
                    for (int i = s - 1; i >= f; i--)
                    {
                        {
                            int y = r.Bottom - (pos + 1) * (font.LineSpacing + 0);

                            string msg = b[i].Text;
                            string pre = "";
                            ConsoleChannel ch = channels[b[i].Channel];

                            if ((messageFormat & ConsoleMessageFormats.ChannelName) == ConsoleMessageFormats.ChannelName)
                            {
                                pre += string.Format("[{0}]", channels[b[i].Channel].Name);
                            }
                            if ((messageFormat & ConsoleMessageFormats.TimeStamp) == ConsoleMessageFormats.TimeStamp)
                            {
                                pre = string.Format("[{0}]", b[i].Time.ToLongTimeString()) + pre;
                            }

                            if (pre != "") msg = pre + ": " + msg;

                            UserInterfaceManager.Renderer.DrawString(font, msg, 4, y, ch.Color);
                            pos += 1;
                        }
                    }
                }
            }
        } // ClientArea_Draw

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            int h = textMain.Visible ? (textMain.Height + 1) : 0;
            Rectangle r = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - h);
            base.DrawControl(r);
        } // DrawControl

        #endregion

        #region Position Controls

        private void PositionControls()
        {
            if (textMain != null)
            {
                textMain.Left = channelsVisible ? cmbMain.Width + 1 : 0;
                textMain.Width = channelsVisible ? Width - cmbMain.Width - 1 : Width;

                if (textBoxVisible)
                {
                    ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.ClientMargins.Top + 4, sbVert.Width + 6, textMain.Height + 4);
                    sbVert.Height = Height - textMain.Height - 5;
                }
                else
                {
                    ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.ClientMargins.Top + 4, sbVert.Width + 6, 2);
                    sbVert.Height = Height - 4;
                }
                Invalidate();
            }
        } // PositionControls

        #endregion

        #region Methods

        private void TextMain_FocusGained(object sender, EventArgs e)
        {
            ConsoleChannel ch = channels[cmbMain.Text];
            if (ch != null) textMain.TextColor = ch.Color;
        } // TextMain_FocusGained

        private void TextMain_KeyDown(object sender, KeyEventArgs e)
        {
            SendMessage(e);
        } // TextMain_KeyDown

        private void SendMessage(EventArgs x)
        {
            KeyEventArgs k = new KeyEventArgs();

            if (x is KeyEventArgs) k = x as KeyEventArgs;

            ConsoleChannel ch = channels[cmbMain.Text];
            if (ch != null)
            {
                textMain.TextColor = ch.Color;

                string message = textMain.Text;
                if ((k.Key == Microsoft.Xna.Framework.Input.Keys.Enter) && !string.IsNullOrEmpty(message))
                {
                    x.Handled = true;

                    ConsoleMessageEventArgs me = new ConsoleMessageEventArgs(new ConsoleMessage(message, ch.Index));
                    OnMessageSent(me);

                    buffer.Add(new ConsoleMessage(me.Message.Text, me.Message.Channel));

                    textMain.Text = "";
                    ClientArea.Invalidate();

                    CalcScrolling();
                }
            }
        } // SendMessage

        private void OnMessageSent(ConsoleMessageEventArgs e)
        {
            if (MessageSent != null) MessageSent.Invoke(this, e);
        } // OnMessageSent

        private void Channels_ItemAdded(object sender, EventArgs e)
        {
            cmbMain.Items.Clear();
            foreach (ConsoleChannel t in channels)
            {
                cmbMain.Items.Add(t.Name);
            }
        } // Channels_ItemAdded

        private void Channels_ItemRemoved(object sender, EventArgs e)
        {
            cmbMain.Items.Clear();
            foreach (ConsoleChannel t in channels)
            {
                cmbMain.Items.Add(t.Name);
            }
        } // Channels_ItemRemoved

        private void Buffer_ItemAdded(object sender, EventArgs e)
        {
            CalcScrolling();
            ClientArea.Invalidate();
        } // Buffer_ItemAdded

        private void CalcScrolling()
        {
            if (sbVert != null)
            {
                int line = SkinInformation.Layers[0].Text.Font.Font.LineSpacing;
                int c = GetFilteredBuffer(filter).Count;
                int p = (int)Math.Ceiling(ClientArea.ClientHeight / (float)line);

                sbVert.Range = c == 0 ? 1 : c;
                sbVert.PageSize = c == 0 ? 1 : p;
                sbVert.Value = sbVert.Range;
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

        private EventedList<ConsoleMessage> GetFilteredBuffer(List<byte> _filter)
        {
            EventedList<ConsoleMessage> ret = new EventedList<ConsoleMessage>();

            if (_filter.Count > 0)
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (_filter.Contains(buffer[i].Channel))
                    {
                        ret.Add(buffer[i]);
                    }
                }
                return ret;
            }
            return buffer;
        } // GetFilteredBuffer

        #endregion

    } // Console
} // XNAFinalEngine.UserInterface
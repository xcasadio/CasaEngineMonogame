using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace Editor.WinForm
{
    /// <summary>
    /// Input box with specific behaviour
    /// </summary>
    public partial class InputBox
        : Form
    {

        public enum TextType
        {
            Normal,
            NumericInteger,
            NumericFloat,
            Mail
        }



        private TextType m_TextType = TextType.Normal;
        private Color m_TextColor = Color.Black;
        private Color m_ReadOnlyTextColor = Color.Gray;
        private string m_ForbiddenChar = string.Empty;
        private string m_ReadOnlyText = string.Empty;
        private EventHandler m_CustomEvent = null;
        private int m_ToolTipDuration = 2000;
        private bool m_CheckSelectionStartActive = true;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public TextType Type
        {
            get { return m_TextType; }
            set
            {
                m_TextType = value;
                richTextBox.TextChanged += new EventHandler(CheckTextType);
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string ReadOnlyText
        {
            get { return m_ReadOnlyText; }
            set
            {
                m_ReadOnlyText = value == null ? string.Empty : value;

                richTextBox.TextChanged -= new EventHandler(CheckReadOnly);

                if (string.IsNullOrEmpty(m_ReadOnlyText) == false)
                {
                    richTextBox.TextChanged += new EventHandler(CheckReadOnly);
                }

                ColorText(m_ReadOnlyText, -1);
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string ForbiddenChar
        {
            get { return m_ForbiddenChar; }
            set
            {
                m_ForbiddenChar = value == null ? string.Empty : value;

                richTextBox.TextChanged -= new EventHandler(CheckForbiddenChar);

                if (string.IsNullOrEmpty(m_ForbiddenChar) == false)
                {
                    richTextBox.TextChanged += new EventHandler(CheckForbiddenChar);
                }
            }
        }

        /// <summary>
        /// Gets the text entered by the user
        /// </summary>
        public string InputText
        {
            get { return richTextBox.Text; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Color ReadOnlyTextColor
        {
            get { return m_ReadOnlyTextColor; }
            set
            {
                m_ReadOnlyTextColor = value;
                ColorText(richTextBox.Text, -1);
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Color TextColor
        {
            get { return m_TextColor; }
            set
            {
                m_TextColor = value;
                ColorText(richTextBox.Text, -1);
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string LabelText
        {
            get { return label.Text; }
            set { label.Text = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        public InputBox()
        {
            InitializeComponent();

            toolTip1.SetToolTip(richTextBox, "essai");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title_"></param>
        /// <param name="labelText_"></param>
        /// <param name="inputText_"></param>
        public InputBox(string title_, string labelText_, string inputText_)
            : this()
        {
            Text = title_;
            label.Text = labelText_;
            richTextBox.Text = inputText_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title_"></param>
        /// <param name="labelText_"></param>
        /// <param name="inputText_"></param>
        /// <param name="textChanged_"></param>
        public InputBox(string title_, string labelText_, string inputText_, EventHandler textChanged_)
            : this(title_, labelText_, inputText_)
        {
            richTextBox.TextChanged += new EventHandler(textChanged_);
            m_CustomEvent = textChanged_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputBox_Shown(object sender, EventArgs e)
        {
            richTextBox.Focus();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckTextType(object sender, EventArgs e)
        {
            switch (m_TextType)
            {
                case TextType.Mail:
                    Regex emailregex = new Regex(@"[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,4}");
                    Match mmatch = emailregex.Match(richTextBox.Text);

                    if (mmatch.Success == false)
                    {
                        //toolTip1.Show("Invalid e-mail address...", richTextBox, m_ToolTipDuration);
                        buttonOK.Enabled = false;
                    }
                    else
                    {
                        buttonOK.Enabled = true;
                    }
                    break;

                    /*case TextType.Numeric:

                        break;*/
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckForbiddenChar(object sender, EventArgs e)
        {
            string text = richTextBox.Text;
            int pos;
            bool found = false;
            int currentSelectionStart = richTextBox.SelectionStart;

            if (string.IsNullOrEmpty(text) == false)
            {
                while ((pos = text.LastIndexOfAny(m_ForbiddenChar.ToCharArray())) != -1)
                {
                    text = text.Remove(pos, 1);
                    found = true;
                }

                if (found == true)
                {
                    ColorText(text, currentSelectionStart);
                    toolTip1.Show("Invalid character " + m_ForbiddenChar, richTextBox, m_ToolTipDuration);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckReadOnly(object sender, EventArgs e)
        {
            int currentSelectionStart = richTextBox.SelectionStart;

            StringBuilder text = new StringBuilder();

            if (string.IsNullOrEmpty(richTextBox.Text) == true && m_ReadOnlyText.Length > 0)
            {
                text.Append(m_ReadOnlyText);
            }
            else if (richTextBox.Text.StartsWith(m_ReadOnlyText) == false)
            {
                string txt = richTextBox.Text;

                text.Append(m_ReadOnlyText);

                if (richTextBox.Text.Length > m_ReadOnlyText.Length)
                {
                    text.Append(txt.Substring(m_ReadOnlyText.Length));
                }
            }
            else
            {
                text.Append(richTextBox.Text);
            }

            ColorText(text.ToString(), currentSelectionStart);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="txt_"></param>
        /// <param name="selectionStart_"></param>
        private void ColorText(string txt_, int selectionStart_)
        {
            richTextBox.Text = txt_;

            m_CheckSelectionStartActive = false;

            if (string.IsNullOrEmpty(m_ReadOnlyText) == false)
            {
                richTextBox.SelectionStart = 0;
                richTextBox.SelectionLength = m_ReadOnlyText.Length;
                richTextBox.SelectionColor = m_ReadOnlyTextColor;
            }

            richTextBox.SelectionStart = m_ReadOnlyText.Length;
            richTextBox.SelectionLength = richTextBox.Text.Length - m_ReadOnlyText.Length;
            richTextBox.SelectionColor = m_TextColor;

            m_CheckSelectionStartActive = true;

            if (selectionStart_ < 0)
            {
                richTextBox.SelectionStart = richTextBox.Text.Length;
            }
            else if (m_ReadOnlyText.Length > 0)
            {
                richTextBox.SelectionStart = selectionStart_ < m_ReadOnlyText.Length ? m_ReadOnlyText.Length : selectionStart_;
            }
            else
            {
                richTextBox.SelectionStart = selectionStart_;
            }

            richTextBox.SelectionLength = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void richTextBox_SelectionChanged(object sender, EventArgs e)
        {
            if (m_CheckSelectionStartActive == false)
            {
                return;
            }

            if (m_ReadOnlyText.Length > 0 && richTextBox.SelectionStart < m_ReadOnlyText.Length)
            {
                richTextBox.SelectionStart = m_ReadOnlyText.Length;
            }
        }


        private void richTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (m_TextType == TextType.NumericInteger || m_TextType == TextType.NumericFloat)
            {
                // N'accepte que du numérique
                if ((e.KeyChar < 48 || e.KeyChar > 57) && e.KeyChar != 8 && e.KeyChar != 13 &&
                    e.KeyChar != '-')
                {
                    e.Handled = true;
                }
                else
                {
                    e.Handled = false;
                }
            }
        }
    }
}

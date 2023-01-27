using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngineCommon.Logger;


namespace Editor.Log
{
    /// <summary>
    /// 
    /// </summary>
    public class LogEditor
        : ILog
    {
        #region Fields

        private RichTextBox m_TextBox;
        private readonly string m_Debug = "[Debug] : ";
        private readonly string m_Warning = "Warning : ";
        private readonly string m_Error = "Error : ";

        #endregion // Fields

        #region Properties

        #endregion // Properties

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textBox_"></param>
        public LogEditor(RichTextBox textBox_)
        {
            if (textBox_ == null)
            {
                throw new ArgumentNullException("LogEditor() : RichTextBox is null");
            }

            m_TextBox = textBox_;
        }

        #endregion // Constructors

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        public void Close()
        {

        }

        delegate void SetWriteCallback(string msg_, System.Drawing.Color color_, bool displayTime_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        private void Write(string msg_, System.Drawing.Color color_, bool displayTime_)
        {
            if (this.m_TextBox.InvokeRequired)
            {
                SetWriteCallback c = new SetWriteCallback(WriteCallback);
                m_TextBox.Invoke(c, new object[] { msg_, color_, displayTime_ });
            }
            else
            {
                WriteCallback(msg_, color_, displayTime_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        private void WriteCallback(string msg_, System.Drawing.Color color_, bool displayTime_)
        {
            if (displayTime_ == true)
            {
                m_TextBox.SelectionColor = System.Drawing.Color.Violet;
                m_TextBox.AppendText(DateTime.Now.ToString("T") + " : ");
            }

            m_TextBox.SelectionColor = color_;
            m_TextBox.AppendText(msg_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        /// <param name="args_"></param>
        public void Write(params object[] args_)
        {
            if (this.m_TextBox.InvokeRequired == false)
            {
                bool first = true;
                string msg = string.Empty;

                if (args_ != null)
                {
                    for (int i = 0; i < args_.Length; i++)
                    {
                        if (args_[i] is System.Drawing.Color)
                        {
                            WriteCallback(msg, (System.Drawing.Color)args_[i], first);
                            first = false;
                        }
                        else if (args_[i] is string)
                        {
                            msg = (string)args_[i];

                            if (i + 1 == args_.Length)
                            {
                                WriteCallback(msg, System.Drawing.Color.Black, first);
                                first = false;
                            }
                            else if (args_[i + 1] is string)
                            {
                                WriteCallback(msg, System.Drawing.Color.Black, first);
                                first = false;
                            }
                        }
                    }
                }
            }
            else
            {
                m_TextBox.Invoke(new Action(()=> Write(args_)));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        public void WriteLineDebug(string msg_)
        {
            Write(m_Debug, msg_ + Environment.NewLine, System.Drawing.Color.Brown);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        public void WriteLineWarning(string msg_)
        {
            Write(m_Warning, System.Drawing.Color.Red,
                msg_ + Environment.NewLine, System.Drawing.Color.Black);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        public void WriteLineError(string msg_)
        {
            Write(m_Error + msg_ + Environment.NewLine, System.Drawing.Color.Red);
        }

        #endregion // Methods
    }
}

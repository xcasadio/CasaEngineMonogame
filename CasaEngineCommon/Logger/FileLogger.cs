using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CasaEngineCommon.Logger
{
	/// <summary>
	/// Log into a file
	/// </summary>
	public class FileLogger
		: ILog
	{
		#region Fields

		StreamWriter m_Stream = null;
        private readonly string m_Debug = "[DEBUG] : ";
		private readonly string m_Warning = "Warning : ";
		private readonly string m_Error = "Error : ";

		#endregion // Fields

		#region Properties

		#endregion // Properties

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		public FileLogger(string fileName_)
		{
			m_Stream = new StreamWriter(fileName_, false);
			m_Stream.AutoFlush = true;
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		public void Close()
		{
			m_Stream.Close();
			m_Stream = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		private void Write(string msg_, bool displayTime_)
		{
			if (displayTime_ == true)
			{
				m_Stream.Write(DateTime.Now.ToString("T") + " : ");
			}

			m_Stream.Write(msg_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		/// <param name="args_"></param>
		public void Write(params object[] args_)
		{
			bool first = true;
			string msg = string.Empty;

			if (args_ != null)
			{
				for (int i = 0; i < args_.Length; i++)
				{
					if (args_[i] is string)
					{
						msg = (string)args_[i];
						Write(msg, first);
						first = false;
					}
				}
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        public void WriteLineDebug(string msg_)
        {
            Write(m_Debug + msg_ + Environment.NewLine);
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		public void WriteLineWarning(string msg_)
		{
			Write(m_Warning + msg_ + Environment.NewLine);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		public void WriteLineError(string msg_)
		{
			Write(m_Error + msg_ + Environment.NewLine);
		}

		#endregion // Methods
	}
}

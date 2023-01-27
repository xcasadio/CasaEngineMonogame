using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngineCommon.Logger
{
	/// <summary>
	/// 
	/// </summary>
    public sealed class LogManager
	{
        /// <summary>
        /// 
        /// </summary>
        public enum LogVerbosity
        {
            Debug,
            Normal,
            None
        }

		#region Fields

        static private LogManager m_Instance = null; 

		private List<ILog> m_Loggers = new List<ILog>();

#if DEBUG
        private LogVerbosity m_Verbosity = LogVerbosity.Debug;
#else
        private LogVerbosity m_Verbosity = LogVerbosity.Normal;
#endif

		#endregion

		#region Properties

        /// <summary>
        /// Gets
        /// </summary>
        static public LogManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new LogManager();
                }
                return m_Instance;
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public LogVerbosity Verbosity
        {
            get { return m_Verbosity; }
            set { m_Verbosity = value; }
        }

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="log_"></param>
		public void AddLogger(ILog log_)
		{
			if (log_ == null)
			{
				throw new ArgumentNullException("LogManager.Instance.AddLogger() : ILog is null");
			}

			m_Loggers.Add(log_);
		}

        /// <summary>
		/// 
		/// </summary>
		/// <param name="log_"></param>
		public void Close()
		{
			foreach (ILog log in m_Loggers)
			{
				log.Close();
			}

            m_Loggers.Clear();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="args_"></param>
		public void Write(params object[] args_)
		{
			foreach (ILog log in m_Loggers)
			{
				log.Write(args_);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="args_"></param>
		public void WriteLine(params object[] args_)
		{
			object[] newArgs;

			if (args_ != null)
			{
				newArgs = new object[args_.Length + 1];

				for (int i = 0; i < args_.Length; i++)
				{
					newArgs[i] = args_[i];
				}
			}
			else
			{
				newArgs = new object[1];
			}

			newArgs[newArgs.Length - 1] = "\n";

			Write(newArgs);
		}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        public void WriteLineDebug(string msg_)
        {
            if (m_Verbosity != LogVerbosity.Debug)
            {
                return;
            }

            foreach (ILog log in m_Loggers)
            {
                log.WriteLineDebug(msg_);
            }
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		public void WriteLineWarning(string msg_)
		{
            if (m_Verbosity == LogVerbosity.None)
            {
                return;
            }

			foreach (ILog log in m_Loggers)
			{
				log.WriteLineWarning(msg_);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		public void WriteLineError(string msg_)
		{
            if (m_Verbosity == LogVerbosity.None)
            {
                return;
            }

			foreach (ILog log in m_Loggers)
			{
				log.WriteLineError(msg_);
			}
		}

		/// <summary>
		/// Write exception into log
		/// </summary>
		/// <param name="e"></param>
		public void WriteException(Exception e, bool writeStackTrace = true)
		{
            if (m_Verbosity == LogVerbosity.None)
            {
                return;
            }

            StringBuilder strBldr = new StringBuilder();

            strBldr.AppendLine(e.Message);

            Exception ex = e;
            string tab = "\t";

            while (ex.InnerException != null)
            {
                strBldr.Append(tab);
                strBldr.AppendLine(ex.Message);
                ex = ex.InnerException;
                tab += "\t";
            }

            if (writeStackTrace == true)
            {
                strBldr.AppendLine(e.StackTrace);
            }

            WriteLineError(strBldr.ToString());
		}

		#endregion
	}
}

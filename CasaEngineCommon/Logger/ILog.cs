using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngineCommon.Logger
{
	/// <summary>
	/// Interface for all Logger
	/// </summary>
	public interface ILog
	{
		/// <summary>
		/// 
		/// </summary>
		void Close();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="args_"></param>
		void Write(params object[] args_);

#if !FINAL
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg_"></param>
        void WriteLineDebug(string msg_);
#endif

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		void WriteLineWarning(string msg_);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg_"></param>
		void WriteLineError(string msg_);
	}
}

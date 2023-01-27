using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using P4API;
using CasaEngineCommon.Logger;
using CasaEngine.Game;
using System.Windows;
using System.Globalization;

namespace CasaEngine.SourceControl
{
    /// <summary>
    /// 
    /// </summary>
    public class P4SourceControl
        : ISourceControl
    {
        #region Fields

		private P4Connection m_P4Connection = null;

        private string[] m_CommandFstatKeys = { 
            "headModTime",
            "clientFile",
            "actionOwner",
            "change",
            "depotFile",
            "headTime",
            "type",
            "headRev",
            "digest",
            "headAction",
            "action",
            "isMapped",
            "fileSize",
            "headType",
            "headChange",
            "path",
            "haveRev"};

        private string[] m_CommandFileKeys = {
            "rev",
            "change",
            "depotFile",        
            "type",
            "action",
            "time"};

        private Dictionary<string, SourceControlKeyWord> KeyWordMapping = new Dictionary<string, SourceControlKeyWord>();

        private bool m_ValidWorkspaceDirectory = false;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scManager_"></param>
        public P4SourceControl()
		{
			m_P4Connection = new P4Connection();

            KeyWordMapping.Add("action", SourceControlKeyWord.Action);
            KeyWordMapping.Add("actionOwner", SourceControlKeyWord.ActionOwner);
            KeyWordMapping.Add("change", SourceControlKeyWord.Change);
            KeyWordMapping.Add("clientFile", SourceControlKeyWord.ClientFile);
            KeyWordMapping.Add("depotFile", SourceControlKeyWord.DepotFile);
            KeyWordMapping.Add("fileSize", SourceControlKeyWord.FileSize);
            KeyWordMapping.Add("haveRev", SourceControlKeyWord.HaveRev);
            KeyWordMapping.Add("headAction", SourceControlKeyWord.HeadAction);
            KeyWordMapping.Add("headChange", SourceControlKeyWord.HeadChange);
            KeyWordMapping.Add("headModTime", SourceControlKeyWord.HeadModTime);
            KeyWordMapping.Add("headTime", SourceControlKeyWord.HeadTime);
            KeyWordMapping.Add("headRev", SourceControlKeyWord.HeadRev);
            KeyWordMapping.Add("HeadTime", SourceControlKeyWord.HeadTime);
            KeyWordMapping.Add("HeadType", SourceControlKeyWord.HeadType);
            KeyWordMapping.Add("isMapped", SourceControlKeyWord.IsMapped);
            KeyWordMapping.Add("digest", SourceControlKeyWord.MD5);
            KeyWordMapping.Add("otherLock", SourceControlKeyWord.OtherLock);
            KeyWordMapping.Add("path", SourceControlKeyWord.Path);
            KeyWordMapping.Add("resolved", SourceControlKeyWord.Resolved);
            KeyWordMapping.Add("type", SourceControlKeyWord.Type);
            KeyWordMapping.Add("unresolved", SourceControlKeyWord.Unresolved);
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="command_"></param>
		/// <param name="args_"></param>
		/// <returns></returns>
		private bool Run(string command_, params string[] args_)
		{
            P4RecordSet set = RunEx(command_, args_);
			return set == null ? false : set.Errors.Count() == 0;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="command_"></param>
		/// <param name="args_"></param>
		/// <returns></returns>
		private P4RecordSet RunEx(string command_, params string[] args_)
		{
            if (IsValidConnection() == false)
            {
                throw new InvalidOperationException("You are not connected to Perforce!");
            }

            P4RecordSet set = null;

            try
            {
                set = m_P4Connection.Run(command_, args_);

                if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
                {
                    foreach (string warning in set.Warnings)
                    {
                        LogManager.Instance.WriteLineWarning("{Perforce : " + command_ + args_ + " } " + warning);
                    }
                }

                foreach (string error in set.Errors)
                {
                    LogManager.Instance.WriteLineError("{Perforce} " + error);
                }

#if DEBUG
                foreach (string msg in set.Messages)
                {
                    LogManager.Instance.WriteLineWarning("{Perforce} " + msg);
                }
#endif               
            }
            catch (System.Exception e)
            {
                LogManager.Instance.WriteException(e, false);
            }

            return set;
		}

		/// <summary>
		/// 
		/// </summary>
		public void Connect()
        {
#if UNITTEST
            return;
#endif
            m_ValidWorkspaceDirectory = false;

            m_P4Connection.Disconnect();
            m_P4Connection.Port = SourceControlManager.Instance.Server;
            m_P4Connection.User = SourceControlManager.Instance.User;
            m_P4Connection.Client = SourceControlManager.Instance.Workspace;
            m_P4Connection.CWD = SourceControlManager.Instance.CWD; //GameInfo.Instance.ProjectManager.ProjectPath;
            m_P4Connection.Password = SourceControlManager.Instance.Password;

            try
            {
                m_P4Connection.Connect();

                if (m_P4Connection.IsValidConnection(true, true) == true)
                {
                    P4RecordSet set = m_P4Connection.Run("workspaces", "-u", SourceControlManager.Instance.User);

                    if (set != null)
                    {
                        foreach (P4Record r in set.Records)
                        {
                            if (r.Fields["client"].Equals(SourceControlManager.Instance.Workspace) == true)
                            {
                                if (m_P4Connection.CWD.Equals(r.Fields["Root"]) == true)
                                {
                                    m_ValidWorkspaceDirectory = true;
                                    LogManager.Instance.WriteLine("Connected to Perforce server");
                                    return;
                                }
                            }
                        }

                        LogManager.Instance.WriteLineError("[Perforce] The directory '" + Engine.Instance.ProjectManager.ProjectPath + "' is not in the client view ('" + SourceControlManager.Instance.Workspace + "').");
                    }
                    else
                    {
                        LogManager.Instance.WriteLineError("[Perforce] Can't execute the command perforce : 'workspaces -u " + SourceControlManager.Instance.User + "'");
                    }
                }
                else
                {
                    LogManager.Instance.WriteLineError("[Perforce] Can't connect to Perforce server");
                }                
            }
            catch (System.Exception e)
            {
                LogManager.Instance.WriteException(e, false);
            }
		}

		/// <summary>
		/// 
		/// </summary>
		public void Disconnect()
		{
			m_P4Connection.Disconnect();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public bool IsValidConnection()
        {
#if UNITTEST
            return false;
#endif

/*#if DEBUG
            return false;
#endif*/
            return m_ValidWorkspaceDirectory && m_P4Connection.IsValidConnection(true, true);
		}

		/// <summary>
		/// Get status of one file
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
        public Dictionary<string, Dictionary<SourceControlKeyWord, string>> FileStatus(string[] filesName_)
		{
            Dictionary<string, Dictionary<SourceControlKeyWord, string>> res = new Dictionary<string, Dictionary<SourceControlKeyWord, string>>();
            Dictionary<SourceControlKeyWord, string> fileRes;
            //P4RecordSet set = RunEx("files", filesName_);
            //P4RecordSet set = RunEx("fstat", "-Ol", "-Rc", "//" + SourceControlManager.Instance.Workspace + "/...");
            //P4RecordSet set = RunEx("fstat", "-Olhp", "//LostKingdom_Workspace_Editor_XC/...");
            P4RecordSet set = RunEx("fstat", filesName_);

            if (set != null)
            {
                foreach(P4Record record in set.Records)
                {
                    fileRes = new Dictionary<SourceControlKeyWord, string>();

                    foreach (string key in record.Fields.Keys)
                    {
                        if (KeyWordMapping.ContainsKey(key) == true)
                        {
                            fileRes.Add(KeyWordMapping[key], record.Fields[key]);
                        }
                    }

                    string file = record.Fields["clientFile"];
                    res.Add(file, fileRes);
                }
            }

			return res;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        public bool RevertFile(string fileName_)
        {
            return Run("revert", fileName_);
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="changeListNum_"></param>
		/// <returns></returns>
		public bool Submit(int changeListNum_)
		{
            //P4PendingChangelist pc = new P4PendingChangelist();
            //pc.Number = 
            //pc.Description = "";
			//TODO : verif description
			/*return Run("submit", new string[] 
				{
					"-c",
					changeListNum_.ToString()
				});*/
            return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
		public bool CheckOut(string fileName_)
		{
            //sync
			return Run("edit", fileName_);
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Sync(string fileName_)
        {
            return Run("sync", fileName_);
        }

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public bool SyncAll()
		{
			return Run("sync");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
		public bool LockFile(string fileName_)
		{
            //p4 [g-opts] lock [-c changelist] [file ...] 
			return Run("lock", fileName_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
		public bool UnlockFile(string fileName_)
		{
            //p4 [g-opts] unlock [-c changelist | -s shelvedchange ] [-f] file...
			return Run("unlock", fileName_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
		public bool MarkFileForDelete(string fileName_)
		{
			return Run("delete", "-v", fileName_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fileName_"></param>
		/// <returns></returns>
		public bool MarkFileForAdd(string fileName_)
		{
			return Run("add", "-f", fileName_);
		}

		#endregion
    }
}

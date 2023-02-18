using CasaEngine.Core.Logger;
using P4API;

namespace CasaEngine.Editor.SourceControl
{
    public class P4SourceControl
        : ISourceControl
    {

        private readonly P4Connection _p4Connection;

        private string[] _commandFstatKeys = {
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

        private string[] _commandFileKeys = {
            "rev",
            "change",
            "depotFile",
            "type",
            "action",
            "time"};

        private readonly Dictionary<string, SourceControlKeyWord> _keyWordMapping = new();

        private bool _validWorkspaceDirectory;





        public P4SourceControl()
        {
            _p4Connection = new P4Connection();

            _keyWordMapping.Add("action", SourceControlKeyWord.Action);
            _keyWordMapping.Add("actionOwner", SourceControlKeyWord.ActionOwner);
            _keyWordMapping.Add("change", SourceControlKeyWord.Change);
            _keyWordMapping.Add("clientFile", SourceControlKeyWord.ClientFile);
            _keyWordMapping.Add("depotFile", SourceControlKeyWord.DepotFile);
            _keyWordMapping.Add("fileSize", SourceControlKeyWord.FileSize);
            _keyWordMapping.Add("haveRev", SourceControlKeyWord.HaveRev);
            _keyWordMapping.Add("headAction", SourceControlKeyWord.HeadAction);
            _keyWordMapping.Add("headChange", SourceControlKeyWord.HeadChange);
            _keyWordMapping.Add("headModTime", SourceControlKeyWord.HeadModTime);
            _keyWordMapping.Add("headTime", SourceControlKeyWord.HeadTime);
            _keyWordMapping.Add("headRev", SourceControlKeyWord.HeadRev);
            _keyWordMapping.Add("HeadTime", SourceControlKeyWord.HeadTime);
            _keyWordMapping.Add("HeadType", SourceControlKeyWord.HeadType);
            _keyWordMapping.Add("isMapped", SourceControlKeyWord.IsMapped);
            _keyWordMapping.Add("digest", SourceControlKeyWord.Md5);
            _keyWordMapping.Add("otherLock", SourceControlKeyWord.OtherLock);
            _keyWordMapping.Add("path", SourceControlKeyWord.Path);
            _keyWordMapping.Add("resolved", SourceControlKeyWord.Resolved);
            _keyWordMapping.Add("type", SourceControlKeyWord.Type);
            _keyWordMapping.Add("unresolved", SourceControlKeyWord.Unresolved);
        }



        private bool Run(string command, params string[] args)
        {
            var set = RunEx(command, args);
            return set == null ? false : set.Errors.Count() == 0;
        }

        private P4RecordSet RunEx(string command, params string[] args)
        {
            if (IsValidConnection() == false)
            {
                throw new InvalidOperationException("You are not connected to Perforce!");
            }

            P4RecordSet set = null;

            try
            {
                set = _p4Connection.Run(command, args);

                if (LogManager.Instance.Verbosity == LogManager.LogVerbosity.Debug)
                {
                    foreach (var warning in set.Warnings)
                    {
                        LogManager.Instance.WriteLineWarning("{Perforce : " + command + args + " } " + warning);
                    }
                }

                foreach (var error in set.Errors)
                {
                    LogManager.Instance.WriteLineError("{Perforce} " + error);
                }

#if DEBUG
                foreach (var msg in set.Messages)
                {
                    LogManager.Instance.WriteLineWarning("{Perforce} " + msg);
                }
#endif               
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e, false);
            }

            return set;
        }

        public void Connect()
        {
#if UNITTEST
            return;
#endif
            _validWorkspaceDirectory = false;

            _p4Connection.Disconnect();
            _p4Connection.Port = SourceControlManager.Instance.Server;
            _p4Connection.User = SourceControlManager.Instance.User;
            _p4Connection.Client = SourceControlManager.Instance.Workspace;
            _p4Connection.CWD = SourceControlManager.Instance.Cwd; //GameInfo.Instance.ProjectManager.ProjectPath;
            _p4Connection.Password = SourceControlManager.Instance.Password;

            try
            {
                _p4Connection.Connect();

                if (_p4Connection.IsValidConnection(true, true))
                {
                    var set = _p4Connection.Run("workspaces", "-u", SourceControlManager.Instance.User);

                    if (set != null)
                    {
                        foreach (var r in set.Records)
                        {
                            if (r.Fields["client"].Equals(SourceControlManager.Instance.Workspace))
                            {
                                if (_p4Connection.CWD.Equals(r.Fields["Root"]))
                                {
                                    _validWorkspaceDirectory = true;
                                    LogManager.Instance.WriteLine("Connected to Perforce server");
                                    return;
                                }
                            }
                        }

                        LogManager.Instance.WriteLineError("[Perforce] The directory '" + Framework.Game.Engine.Instance.ProjectManager.ProjectPath + "' is not in the client view ('" + SourceControlManager.Instance.Workspace + "').");
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
            catch (Exception e)
            {
                LogManager.Instance.WriteException(e, false);
            }
        }

        public void Disconnect()
        {
            _p4Connection.Disconnect();
        }

        public bool IsValidConnection()
        {
#if UNITTEST
            return false;
#endif

            /*#if DEBUG
                        return false;
            #endif*/
            return _validWorkspaceDirectory && _p4Connection.IsValidConnection(true, true);
        }

        public Dictionary<string, Dictionary<SourceControlKeyWord, string>> FileStatus(string[] filesName)
        {
            var res = new Dictionary<string, Dictionary<SourceControlKeyWord, string>>();
            Dictionary<SourceControlKeyWord, string> fileRes;
            //P4RecordSet set = RunEx("files", filesName_);
            //P4RecordSet set = RunEx("fstat", "-Ol", "-Rc", "//" + SourceControlManager.Instance.Workspace + "/...");
            //P4RecordSet set = RunEx("fstat", "-Olhp", "//LostKingdo_Workspace_Editor_XC/...");
            var set = RunEx("fstat", filesName);

            if (set != null)
            {
                foreach (var record in set.Records)
                {
                    fileRes = new Dictionary<SourceControlKeyWord, string>();

                    foreach (var key in record.Fields.Keys)
                    {
                        if (_keyWordMapping.ContainsKey(key))
                        {
                            fileRes.Add(_keyWordMapping[key], record.Fields[key]);
                        }
                    }

                    var file = record.Fields["clientFile"];
                    res.Add(file, fileRes);
                }
            }

            return res;
        }

        public bool RevertFile(string fileName)
        {
            return Run("revert", fileName);
        }

        public bool Submit(int changeListNu)
        {
            //P4PendingChangelist pc = new P4PendingChangelist();
            //pc.Number = 
            //pc.Description = "";
            //TODO : verif description
            /*return Run("submit", new string[] 
				{
					"-c",
					changeListNu_.ToString()
				});*/
            return false;
        }

        public bool CheckOut(string fileName)
        {
            //sync
            return Run("edit", fileName);
        }

        public bool Sync(string fileName)
        {
            return Run("sync", fileName);
        }

        public bool SyncAll()
        {
            return Run("sync");
        }

        public bool LockFile(string fileName)
        {
            //p4 [g-opts] lock [-c changelist] [file ...] 
            return Run("lock", fileName);
        }

        public bool UnlockFile(string fileName)
        {
            //p4 [g-opts] unlock [-c changelist | -s shelvedchange ] [-f] file...
            return Run("unlock", fileName);
        }

        public bool MarkFileForDelete(string fileName)
        {
            return Run("delete", "-v", fileName);
        }

        public bool MarkFileForAdd(string fileName)
        {
            return Run("add", "-f", fileName);
        }

    }
}

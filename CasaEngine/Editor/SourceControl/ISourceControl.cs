namespace CasaEngine.SourceControl
{
    public interface ISourceControl
    {
        void Connect();

        void Disconnect();

        bool IsValidConnection();

        Dictionary<string, Dictionary<SourceControlKeyWord, string>> FileStatus(string[] filesName_);

        bool RevertFile(string fileName_);

        //file history

        //diff/merge

        //ChangeList

        bool Submit(int changeListNum_);

        bool CheckOut(string fileName_);

        bool Sync(string fileName_);

        bool SyncAll();

        bool LockFile(string fileName_);

        bool UnlockFile(string fileName_);

        bool MarkFileForDelete(string fileName_);

        bool MarkFileForAdd(string fileName_);
    }
}

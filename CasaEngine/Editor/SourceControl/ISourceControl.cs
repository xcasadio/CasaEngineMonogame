namespace CasaEngine.Editor.SourceControl
{
    public interface ISourceControl
    {
        void Connect();

        void Disconnect();

        bool IsValidConnection();

        Dictionary<string, Dictionary<SourceControlKeyWord, string>> FileStatus(string[] filesName);

        bool RevertFile(string fileName);

        //file history

        //diff/merge

        //ChangeList

        bool Submit(int changeListNu);

        bool CheckOut(string fileName);

        bool Sync(string fileName);

        bool SyncAll();

        bool LockFile(string fileName);

        bool UnlockFile(string fileName);

        bool MarkFileForDelete(string fileName);

        bool MarkFileForAdd(string fileName);
    }
}

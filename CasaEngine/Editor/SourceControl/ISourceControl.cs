using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.SourceControl
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISourceControl
    {
        /// <summary>
        /// 
        /// </summary>
        void Connect();

        /// <summary>
        /// 
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        bool IsValidConnection();

        /// <summary>
        /// Gets status of a list of files
        /// </summary>
        /// <param name="filesName_">array of filename</param>
        /// <returns>string : file path
        /// SourceControlKeyWord : status parameter key word
        /// string : status parameter value</returns>
        Dictionary<string, Dictionary<SourceControlKeyWord, string>> FileStatus(string[] filesName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool RevertFile(string fileName_);

        //file history

        //diff/merge

        //ChangeList

        /// <summary>
        /// 
        /// </summary>
        /// <param name="changeListNum_"></param>
        /// <returns></returns>
        bool Submit(int changeListNum_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool CheckOut(string fileName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool Sync(string fileName_);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        bool SyncAll();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool LockFile(string fileName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool UnlockFile(string fileName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool MarkFileForDelete(string fileName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        bool MarkFileForAdd(string fileName_);
    }
}

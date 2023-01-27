using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.SourceControl
{
    /// <summary>
    /// 
    /// </summary>
    public enum SourceControlKeyWord
    {
        /// <summary>
        /// File in depot but not mapped by workspace view 
        /// </summary>
        IsMapped,
        /// <summary>
        /// File size
        /// </summary>
        FileSize,
        /// <summary>
        /// local path to file
        /// </summary>
        ClientFile,
        /// <summary>
        /// MD5 of a file
        /// </summary>
        MD5,
        /// <summary>
        /// Open action, if opened in your workspace (add, edit, delete, branch, or integrate)
        /// </summary>
        Action,
        /// <summary>
        /// the user who opened the file, if open 
        /// </summary>
        ActionOwner,
        /// <summary>
        /// Head revision modification time, if in depot. Time is measured in seconds since 00:00:00 UTC, January 1, 1970 
        /// </summary>
        HeadTime,
        /// <summary>
        /// Head revision modification time, if in depot. Time is measured in seconds since 00:00:00 UTC, January 1, 1970 
        /// </summary>
        HeadModTime,
        /// <summary>
        /// action taken at head revision, if in depot (add, edit, delete, branch, or integrate)
        /// </summary>
        HeadAction,
        /// <summary>
        /// head revision number, if in depot
        /// </summary>
        HeadRev,
        /// <summary>
        /// head revision type, if in depot (text, binary, text+k, ...)
        /// </summary>
        HeadType,
        /// <summary>
        /// head revision changelist number, if in depot 
        /// </summary>
        HeadChange,
        /// <summary>
        /// revision last synced to workspace, if on workspace 
        /// </summary>
        HaveRev,
        /// <summary>
        /// depot path to file 
        /// </summary>
        DepotFile,
        /// <summary>
        /// local path to file
        /// </summary>
        Path,
        /// <summary>
        /// open changelist number, if opened in your workspace
        /// </summary>
        Change,
        /// <summary>
        /// open type, if opened in your workspace (text, binary, text+k, ...)
        /// </summary>
        Type,
        /// <summary>
        /// present and set to null if another user has the file locked, otherwise not present
        /// </summary>
        OtherLock,
        /// <summary>
        /// the number, if any, of resolved integration records 
        /// </summary>
        Resolved,
        /// <summary>
        /// the number, if any, of unresolved integration records 
        /// </summary>
        Unresolved,           
    }
}

namespace Editor.SourceControl
{
    /// <summary>
    /// 
    /// </summary>
    public enum SourceControlIcon
    {
        /// <summary>
        /// File in depot but not mapped by workspace view 
        /// </summary>
        FolderNotMapped = 0,
        /// <summary>
        /// File open for add by you (red "+") 
        /// </summary>
        FileAdd = 1,
        /// <summary>
        /// File open for add in other workspace (blue "+") 
        /// </summary>
        FileAddOtherWS = 2,
        /// <summary>
        /// File open for branch 
        /// </summary>
        FileBranch = 3,
        /// <summary>
        /// File open for delete by you (red "x") 
        /// </summary>
        FileToDelete = 4,
        /// <summary>
        /// File open for delete by other user (blue "x") 
        /// </summary>
        FileDeleteByOther = 5,
        /// <summary>
        /// File deleted in depot 
        /// </summary>
        FileDeleted = 6,
        /// <summary>
        /// File in workspace differs from head revision
        /// </summary>
        FileDiffers = 7,
        /// <summary>
        /// File open for edit by you (red check mark) 
        /// </summary>
        FileEditHead = 8,
        /// <summary>
        /// File open for edit by other user (blue check mark) 
        /// </summary>
        FileEditOther = 9,
        /// <summary>
        /// File open for integrate (will need resolve) 
        /// </summary>
        FileIntegrate = 10,
        /// <summary>
        /// File locked by you 
        /// </summary>
        FileLock = 11,
        /// <summary>
        /// File locked by other user 
        /// </summary>
        FileLockOther = 12,
        /// <summary>
        /// File needs to be resolved 
        /// </summary>
        FileNeedsResolve = 13,
        /// <summary>
        /// File in depot but not mapped by workspace view 
        /// </summary>
        FileNotMapped = 14,
        /// <summary>
        /// File synced to previous revision 
        /// </summary>
        FileNotSync = 15,
        /// <summary>
        /// File synced to head revision 
        /// </summary>
        FileSync = 16,
        /// <summary>
        /// File in depot 
        /// </summary>
        FileInDepot = 17,
        /// <summary>
        /// File in workspace but not in depot 
        /// </summary>
        FileInWSButNotInDepot = 18,
        /// <summary>
        /// File open for move/rename, "x" indicates source 
        /// </summary>
        FileMoved = 19,
        /// <summary>
        /// File open for move/rename, "+" indicates target
        /// </summary>
        FileMoveTarget = 20,
        /// <summary>
        /// Symbolic link 
        /// </summary>
        SymbolicLink = 21,
        /// <summary>
        /// Pending changelist tab: Pending changelist that contains files requiring resolve.
        /// </summary>        
        PendingListWithFileNeedResolve = 22,
        /// <summary>
        /// (Yellow folder) A folder in your client workspace
        /// </summary>    
        FolderWorkspace = 27,
        /// <summary>
        /// (Red folder) A folder in the Perforce depot
        /// </summary>  
        FolderDepot = 30
    }
}

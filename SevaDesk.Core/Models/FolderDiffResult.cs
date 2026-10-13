namespace SevaDesk.Core.Models;

public enum FolderDiffType
{
    Added,
    Modified,
    Deleted
}

public class FolderDiffItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public FolderDiffType DiffType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}

public class FolderDiffResult
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string WorkingFolderPath { get; set; } = string.Empty;
    public string BackupFolderPath { get; set; } = string.Empty;

    public List<FolderDiffItem> AddedFiles { get; set; } = [];
    public List<FolderDiffItem> ModifiedFiles { get; set; } = [];
    public List<FolderDiffItem> DeletedFiles { get; set; } = [];

    public int AddedCount => AddedFiles.Count;
    public int ModifiedCount => ModifiedFiles.Count;
    public int DeletedCount => DeletedFiles.Count;
    public int TotalChangesCount => AddedCount + ModifiedCount + DeletedCount;
    public bool HasChanges => TotalChangesCount > 0;

    public List<FolderDiffItem> AllDiffs
    {
        get
        {
            var list = new List<FolderDiffItem>();
            list.AddRange(AddedFiles);
            list.AddRange(ModifiedFiles);
            list.AddRange(DeletedFiles);
            return list;
        }
    }
}

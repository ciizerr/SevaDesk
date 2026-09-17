using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IFolderManager
{
    string BaseDirectory { get; }
    string BackupDirectory { get; }
    void SetBaseDirectory(string newPath);
    void SetBackupDirectory(string newPath);
    Task SyncToWorkingAsync(string customerName, string customerCode);
    Task SyncToBackupAsync(string customerName, string customerCode);
    string GetCustomerFolderPath(string customerName, string customerCode);
    string EnsureCustomerWorkingFolder(string customerName, string customerCode);
    string EnsureApplicationSubfolder(string customerFolderPath, string applicationName);
    IEnumerable<string> GetApplicationSubfolders(string customerFolderPath);
    FolderStats GetFolderStats(string folderPath);
    void OpenFolderInExplorer(string folderPath);
    IEnumerable<FolderFileItem> GetFolderFiles(string folderPath);
    bool RenameFile(string oldFullPath, string newFileName, out string newFullPath, out string errorMessage);
    bool DeleteFile(string filePath, out string errorMessage);
    void OpenFileWithDefaultApp(string filePath);
}

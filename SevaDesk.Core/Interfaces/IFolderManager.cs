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
    string GetCustomerBackupFolderPath(string customerName, string customerCode);
    string GetEffectiveCustomerFolderPath(string customerName, string customerCode, out bool isBackup);
    string EnsureCustomerWorkingFolder(string customerName, string customerCode);
    string EnsureApplicationSubfolder(string customerFolderPath, string applicationName);
    IEnumerable<string> GetApplicationSubfolders(string customerFolderPath);
    FolderStats GetFolderStats(string folderPath);
    void OpenFolderInExplorer(string folderPath);
    IEnumerable<FolderFileItem> GetFolderFiles(string folderPath);
    IEnumerable<FolderGroup> GetFolderFilesGrouped(string customerFolderPath);
    bool RenameFile(string oldFullPath, string newFileName, out string newFullPath, out string errorMessage);
    bool DeleteFile(string filePath, out string errorMessage);
    void OpenFileWithDefaultApp(string filePath);
    bool CleanUpEmptyCustomerWorkingFolder(string customerName, string customerCode);
    bool CleanUpEmptyApplicationSubfolder(string customerFolderPath, string applicationName);
    bool DeleteCustomerWorkingFolder(string customerName, string customerCode);
    bool DeleteCustomerBackupFolder(string customerName, string customerCode);
}


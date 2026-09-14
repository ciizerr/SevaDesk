using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IFolderManager
{
    string BaseDirectory { get; }
    void SetBaseDirectory(string newPath);
    string GetCustomerFolderPath(string customerName, string customerCode);
    string EnsureCustomerWorkingFolder(string customerName, string customerCode);
    string EnsureApplicationSubfolder(string customerFolderPath, string applicationName);
    IEnumerable<string> GetApplicationSubfolders(string customerFolderPath);
    FolderStats GetFolderStats(string folderPath);
    void OpenFolderInExplorer(string folderPath);
}

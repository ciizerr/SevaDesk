using SevaDesk.Core.Models;

namespace SevaDesk.Core.Interfaces;

public interface IFolderManager
{
    string BaseDirectory { get; }
    void SetBaseDirectory(string newPath);
    string GetCustomerFolderPath(string customerName, string customerCode);
    string EnsureCustomerWorkingFolder(string customerName, string customerCode);
    FolderStats GetFolderStats(string folderPath);
    void OpenFolderInExplorer(string folderPath);
}

namespace SevaDesk.Core.Models;

public class FolderStats
{
    public int UnorganisedCount { get; set; }
    public int SharedDocsCount { get; set; }
    public int ApplicationsCount { get; set; }
    public int ReadyToPrintCount { get; set; }
    public int TotalFiles => UnorganisedCount + SharedDocsCount + ApplicationsCount + ReadyToPrintCount;
}

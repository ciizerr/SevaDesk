namespace SevaDesk_App.Services;

public interface IPickerService
{
    Task<string?> PickFolderAsync();
    Task<string?> PickFileAsync(string[] filters);
    Task<IReadOnlyList<string>> PickMultipleFilesAsync(string[] filters);
    Task<string?> PickSaveFileAsync(string suggestedFileName, string extension, string fileTypeDescription);
}

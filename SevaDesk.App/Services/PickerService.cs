using Windows.Storage.Pickers;

namespace SevaDesk_App.Services;

public class PickerService : IPickerService
{
    private static IntPtr _mainWindowHandle = IntPtr.Zero;

    public static void Initialize(IntPtr hwnd)
    {
        _mainWindowHandle = hwnd;
    }

    private void EnsureHwnd<T>(T picker)
    {
        if (_mainWindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("PickerService not initialized. Call PickerService.Initialize() with the MainWindow handle.");
        
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _mainWindowHandle);
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add("*");
        EnsureHwnd(picker);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickFileAsync(string[] filters)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        foreach (var filter in filters)
        {
            picker.FileTypeFilter.Add(filter);
        }
        EnsureHwnd(picker);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
    
    public async Task<IReadOnlyList<string>> PickMultipleFilesAsync(string[] filters)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        foreach (var filter in filters)
        {
            picker.FileTypeFilter.Add(filter);
        }
        EnsureHwnd(picker);

        var files = await picker.PickMultipleFilesAsync();
        return files.Select(f => f.Path).ToList();
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string extension, string fileTypeDescription)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { ext });
        EnsureHwnd(picker);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _shopName = "SevaDesk Digital Cyber Café";

    [ObservableProperty]
    private string _operatorName = "Ramesh Patel (VLE / Operator)";

    [ObservableProperty]
    private string _contactNumber = "9876543210";

    [ObservableProperty]
    private string _shopUpiVpa = "sevadesk.csc@upi";

    [ObservableProperty]
    private string _workingRootPath = string.Empty;

    [ObservableProperty]
    private int _autoArchiveDays = 30;

    [ObservableProperty]
    private bool _autoOrganizeBluetooth = true;

    [ObservableProperty]
    private bool _enableMicaBackdrop = true;

    [ObservableProperty]
    private string _defaultBwPrinter = "HP LaserJet Pro M404dn";

    [ObservableProperty]
    private string _defaultColorPrinter = "Epson EcoTank L8050 Photo";

    [ObservableProperty]
    private decimal _bwPageRate = 5;

    [ObservableProperty]
    private decimal _colorPageRate = 20;

    [ObservableProperty]
    private string _appVersion = "SevaDesk v1.0.0 (Windows 11 Native WinUI 3 / .NET 8)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public SettingsViewModel()
    {
        WorkingRootPath = AppServices.FolderManager.BaseDirectory;
    }

    [RelayCommand]
    public void OpenWorkingFolder()
    {
        AppServices.FolderManager.OpenFolderInExplorer(WorkingRootPath);
    }

    [RelayCommand]
    public void SaveSettings()
    {
        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        ShopName = "SevaDesk Digital Cyber Café";
        ShopUpiVpa = "sevadesk.csc@upi";
        AutoArchiveDays = 30;
        AutoOrganizeBluetooth = true;
        StatusMessage = "Defaults restored.";
    }
}

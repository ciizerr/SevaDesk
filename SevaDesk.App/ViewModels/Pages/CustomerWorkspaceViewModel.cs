using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class CustomerWorkspaceViewModel : StatusViewModel
{
    [ObservableProperty]
    private Customer _customer = default!;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<Session> _sessions = [];

    [ObservableProperty]
    private ObservableCollection<Payment> _payments = [];

    [ObservableProperty]
    private ObservableCollection<FolderFileItem> _folderFiles = [];

    [ObservableProperty]
    private FolderStats _folderStats = new();

    public CustomerWorkspaceViewModel()
    {
    }

    public async Task InitializeAsync(Customer customer)
    {
        Customer = customer;
        await LoadCustomerDetailsAsync();
    }

    [RelayCommand]
    private async Task LoadCustomerDetailsAsync()
    {
        if (Customer == null) return;

        Sessions.Clear();
        Payments.Clear();
        FolderFiles.Clear();
        FolderStats = new FolderStats();

        IsLoading = true;
        try
        {
            // Load Sessions
            var sessions = await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id);
            foreach (var s in sessions) Sessions.Add(s);

            // Load Payments
            var payments = await AppServices.Payments.GetCustomerPaymentsAsync(Customer.Id);
            foreach (var p in payments) Payments.Add(p);

            // Load Files
            var folderPath = AppServices.FolderManager.GetCustomerFolderPath(Customer.Name, Customer.Code);
            FolderStats = AppServices.FolderManager.GetFolderStats(folderPath);
            
            var files = AppServices.FolderManager.GetFolderFiles(folderPath);
            foreach (var f in files) FolderFiles.Add(f);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenCustomerFolder()
    {
        if (Customer == null) return;
        var folderPath = AppServices.FolderManager.GetCustomerFolderPath(Customer.Name, Customer.Code);
        AppServices.FolderManager.OpenFolderInExplorer(folderPath);
    }
    
    [RelayCommand]
    public async Task StartSessionAsync()
    {
        if (Customer == null) return;
        await AppServices.Sessions.StartSessionAsync(Customer.Id);
        ShowSuccess($"Session started for {Customer.Name}");
        
        // Refresh sessions list
        Sessions.Clear();
        var sessions = await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id);
        foreach (var s in sessions) Sessions.Add(s);
    }
}

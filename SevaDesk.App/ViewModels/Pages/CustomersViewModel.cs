using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class CustomersViewModel : StatusViewModel
{
    [ObservableProperty]
    private ObservableCollection<Customer> _customers = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCustomersCount;

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private ObservableCollection<Session> _customerSessions = [];

    [ObservableProperty]
    private ObservableCollection<Payment> _customerPayments = [];

    [ObservableProperty]
    private ObservableCollection<FolderFileItem> _customerFolderFiles = [];

    [ObservableProperty]
    private FolderStats _customerFolderStats = new();

    public CustomersViewModel()
    {
    }

    public async Task InitializeAsync()
    {
        await LoadCustomersAsync();
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        IsLoading = true;
        try
        {
            var list = await AppServices.Customers.SearchAsync(SearchQuery);
            Customers.Clear();
            foreach (var c in list)
            {
                Customers.Add(c);
            }
            TotalCustomersCount = Customers.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadCustomersAsync();
    }

    public async Task<Customer> CreateCustomerAsync(string name, string? mobile, string? village, string? idRef, string? notes)
    {
        var customer = new Customer
        {
            Name = name.Trim(),
            Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile.Trim(),
            Village = string.IsNullOrWhiteSpace(village) ? null : village.Trim(),
            IdReference = string.IsNullOrWhiteSpace(idRef) ? null : idRef.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        var created = await AppServices.Customers.CreateAsync(customer);
        Customers.Insert(0, created);
        TotalCustomersCount = Customers.Count;
        return created;
    }

    public Visibility CustomerSelectedVisibility => SelectedCustomer != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CustomerNotSelectedVisibility => SelectedCustomer == null ? Visibility.Visible : Visibility.Collapsed;

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        OnPropertyChanged(nameof(CustomerSelectedVisibility));
        OnPropertyChanged(nameof(CustomerNotSelectedVisibility));
        _ = LoadCustomerDetailsAsync(value);
    }

    private async Task LoadCustomerDetailsAsync(Customer? customer)
    {
        CustomerSessions.Clear();
        CustomerPayments.Clear();
        CustomerFolderFiles.Clear();
        CustomerFolderStats = new FolderStats();

        if (customer == null) return;

        IsLoading = true;
        try
        {
            // Load Sessions
            var sessions = await AppServices.Sessions.GetCustomerSessionsAsync(customer.Id);
            foreach (var s in sessions) CustomerSessions.Add(s);

            // Load Payments
            var payments = await AppServices.Payments.GetCustomerPaymentsAsync(customer.Id);
            foreach (var p in payments) CustomerPayments.Add(p);

            // Load Files
            var folderPath = AppServices.FolderManager.GetCustomerFolderPath(customer.Name, customer.Code);
            CustomerFolderStats = AppServices.FolderManager.GetFolderStats(folderPath);
            
            var files = AppServices.FolderManager.GetFolderFiles(folderPath);
            foreach (var f in files) CustomerFolderFiles.Add(f);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenCustomerFolder()
    {
        if (SelectedCustomer == null) return;
        var folderPath = AppServices.FolderManager.GetCustomerFolderPath(SelectedCustomer.Name, SelectedCustomer.Code);
        AppServices.FolderManager.OpenFolderInExplorer(folderPath);
    }
}

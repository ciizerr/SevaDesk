using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<Customer> _customers = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCustomersCount;

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
        await LoadCustomersAsync();
        return created;
    }
}

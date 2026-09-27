using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using Microsoft.UI.Xaml;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public enum CustomerFilterMode
{
    All = 0,
    Recent = 1,
    Frequent = 2
}

public enum CustomerSortMode
{
    NameAsc = 0,
    NameDesc = 1,
    Newest = 2,
    MostVisits = 3,
    RecentVisit = 4
}

public partial class CustomersViewModel : StatusViewModel
{
    private readonly List<CustomerRowModel> _allMasterCustomers = [];
    private CancellationTokenSource? _statsCts;

    [ObservableProperty]
    private ObservableCollection<CustomerRowModel> _customers = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalCustomersCount;

    [ObservableProperty]
    private int _recentVisitorsCount;

    [ObservableProperty]
    private int _uniqueVillagesCount;

    [ObservableProperty]
    private int _filteredCount;

    [ObservableProperty]
    private CustomerFilterMode _selectedFilter = CustomerFilterMode.All;

    [ObservableProperty]
    private CustomerSortMode _selectedSort = CustomerSortMode.NameAsc;

    // Pagination properties
    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 15;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _hasPreviousPage;

    [ObservableProperty]
    private bool _hasNextPage;

    [ObservableProperty]
    private string _pageRangeText = string.Empty;

    public bool HasCustomers => Customers.Count > 0;
    public bool IsEmptyState => !IsLoading && Customers.Count == 0;

    public CustomersViewModel()
    {
    }

    public async Task InitializeAsync()
    {
        await LoadCustomersAsync();
        await LoadOverviewMetricsAsync();
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        IsLoading = true;
        CancelBackgroundStatsLoading();

        try
        {
            var list = (await AppServices.Customers.SearchAsync(SearchQuery)).ToList();
            _allMasterCustomers.Clear();
            foreach (var c in list)
            {
                _allMasterCustomers.Add(new CustomerRowModel(c));
            }

            TotalCustomersCount = _allMasterCustomers.Count;
            UniqueVillagesCount = _allMasterCustomers
                .Where(c => !string.IsNullOrWhiteSpace(c.Customer.Village))
                .Select(c => c.Customer.Village!.Trim().ToLowerInvariant())
                .Distinct()
                .Count();

            CurrentPage = 1;
            ApplyFilterAndPagination();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasCustomers));
            OnPropertyChanged(nameof(IsEmptyState));
        }
    }

    public async Task LoadOverviewMetricsAsync()
    {
        try
        {
            using var connection = AppServices.Database.CreateConnection();
            await connection.OpenAsync();

            var cutoff = DateTime.UtcNow.AddDays(-30).ToString("o");
            var recentCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(DISTINCT customer_id) FROM sessions WHERE started_at >= @Cutoff",
                new { Cutoff = cutoff });

            RecentVisitorsCount = recentCount;
        }
        catch
        {
            // Fallback: estimate from loaded items
            RecentVisitorsCount = _allMasterCustomers.Count(c => c.Customer.CreatedAt >= DateTime.UtcNow.AddDays(-30));
        }
    }

    public void SetFilter(CustomerFilterMode mode)
    {
        if (SelectedFilter == mode) return;
        SelectedFilter = mode;
        CurrentPage = 1;
        ApplyFilterAndPagination();
    }

    public void SetSort(CustomerSortMode mode)
    {
        if (SelectedSort == mode) return;
        SelectedSort = mode;
        ApplyFilterAndPagination();
    }

    public void SetPageSize(int newSize)
    {
        if (newSize <= 0 || PageSize == newSize) return;
        PageSize = newSize;
        CurrentPage = 1;
        ApplyFilterAndPagination();
    }

    [RelayCommand]
    public void NextPage()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            ApplyFilterAndPagination(keepPage: true);
        }
    }

    [RelayCommand]
    public void PreviousPage()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            ApplyFilterAndPagination(keepPage: true);
        }
    }

    [RelayCommand]
    public void FirstPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            ApplyFilterAndPagination(keepPage: true);
        }
    }

    [RelayCommand]
    public void LastPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            ApplyFilterAndPagination(keepPage: true);
        }
    }

    public void ApplyFilterAndPagination(bool keepPage = false)
    {
        CancelBackgroundStatsLoading();

        // 1. Filter
        IEnumerable<CustomerRowModel> filtered = _allMasterCustomers;

        if (SelectedFilter == CustomerFilterMode.Recent)
        {
            var cutoff = DateTime.Now.AddDays(-30);
            filtered = filtered.Where(r =>
                (r.LastVisitDate.HasValue && r.LastVisitDate.Value >= cutoff) ||
                (r.Customer.CreatedAt >= DateTime.UtcNow.AddDays(-30)));
        }
        else if (SelectedFilter == CustomerFilterMode.Frequent)
        {
            filtered = filtered.Where(r => r.VisitCount >= 2);
        }

        // 2. Sort
        filtered = SelectedSort switch
        {
            CustomerSortMode.NameDesc => filtered.OrderByDescending(r => r.Customer.Name, StringComparer.OrdinalIgnoreCase),
            CustomerSortMode.Newest => filtered.OrderByDescending(r => r.Customer.CreatedAt),
            CustomerSortMode.MostVisits => filtered.OrderByDescending(r => r.VisitCount).ThenBy(r => r.Customer.Name),
            CustomerSortMode.RecentVisit => filtered.OrderByDescending(r => r.LastVisitDate ?? DateTime.MinValue).ThenBy(r => r.Customer.Name),
            _ => filtered.OrderBy(r => r.Customer.Name, StringComparer.OrdinalIgnoreCase)
        };

        var filteredList = filtered.ToList();
        FilteredCount = filteredList.Count;

        // 3. Compute Pagination
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)FilteredCount / PageSize));
        if (!keepPage || CurrentPage > TotalPages)
        {
            CurrentPage = Math.Min(CurrentPage, TotalPages);
        }
        if (CurrentPage < 1) CurrentPage = 1;

        HasPreviousPage = CurrentPage > 1;
        HasNextPage = CurrentPage < TotalPages;

        int startIndex = (CurrentPage - 1) * PageSize;
        var pagedItems = filteredList.Skip(startIndex).Take(PageSize).ToList();

        Customers.Clear();
        foreach (var item in pagedItems)
        {
            Customers.Add(item);
        }

        // 4. Update Range Text
        if (FilteredCount == 0)
        {
            PageRangeText = "No customers found";
        }
        else
        {
            int startDisplay = startIndex + 1;
            int endDisplay = Math.Min(startIndex + PageSize, FilteredCount);
            PageRangeText = $"Showing {startDisplay}–{endDisplay} of {FilteredCount} customers";
        }

        OnPropertyChanged(nameof(HasCustomers));
        OnPropertyChanged(nameof(IsEmptyState));

        // 5. Trigger progressive background stats loading for visible items
        StartProgressiveStatsLoading(pagedItems);
    }

    private void StartProgressiveStatsLoading(List<CustomerRowModel> items)
    {
        _statsCts = new CancellationTokenSource();
        var token = _statsCts.Token;

        _ = Task.Run(async () =>
        {
            foreach (var item in items)
            {
                if (token.IsCancellationRequested) break;
                if (!item.StatsLoaded)
                {
                    await item.LoadStatsAsync();
                    // Brief yield to keep UI dispatch butter-smooth
                    await Task.Delay(25, token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    private void CancelBackgroundStatsLoading()
    {
        try
        {
            if (_statsCts != null)
            {
                _statsCts.Cancel();
                _statsCts.Dispose();
                _statsCts = null;
            }
        }
        catch { }
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
        var row = new CustomerRowModel(created);
        _allMasterCustomers.Insert(0, row);
        TotalCustomersCount = _allMasterCustomers.Count;

        CurrentPage = 1;
        ApplyFilterAndPagination();
        _ = LoadOverviewMetricsAsync();

        return created;
    }
}


using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

/// <summary>
/// Wraps a Customer for the customers list, loading expensive stats lazily in background.
/// </summary>
public partial class CustomerRowModel : ObservableObject
{
    public Customer Customer { get; }

    [ObservableProperty]
    private bool _statsLoaded;

    [ObservableProperty]
    private int _visitCount;

    [ObservableProperty]
    private string _lastVisitText = "—";

    [ObservableProperty]
    private DateTime? _lastVisitDate;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _isLoadingStats;

    public bool HasMobile => !string.IsNullOrWhiteSpace(Customer?.Mobile);
    public bool HasVillage => !string.IsNullOrWhiteSpace(Customer?.Village);
    public bool HasMaskedId => !string.IsNullOrWhiteSpace(Customer?.FormattedMaskedId);
    public string FormattedPhone => !string.IsNullOrWhiteSpace(Customer?.Mobile) ? Customer.Mobile.Trim() : "—";
    public string FormattedVillage => !string.IsNullOrWhiteSpace(Customer?.Village) ? Customer.Village.Trim() : "—";

    public string DigitsOnlyMobile
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Customer?.Mobile)) return string.Empty;
            return new string(Customer.Mobile.Where(char.IsDigit).ToArray());
        }
    }

    public bool CanWhatsApp => DigitsOnlyMobile.Length >= 10;

    public CustomerRowModel(Customer customer)
    {
        Customer = customer;
    }

    /// <summary>Called in background to load stats once then cache.</summary>
    public async Task LoadStatsAsync()
    {
        if (StatsLoaded || IsLoadingStats) return;
        IsLoadingStats = true;
        try
        {
            // Session count + last visit date
            var sessions = (await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id)).ToList();
            VisitCount = sessions.Count;
            var latest = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();
            if (latest != null)
            {
                LastVisitDate = latest.StartedAt.ToLocalTime();
                var days = (DateTime.Now.Date - latest.StartedAt.ToLocalTime().Date).TotalDays;
                if (days == 0) LastVisitText = "Today";
                else if (days == 1) LastVisitText = "Yesterday";
                else if (days < 7) LastVisitText = $"{(int)days}d ago";
                else LastVisitText = latest.StartedAt.ToLocalTime().ToString("dd MMM yyyy");
            }
            else
            {
                LastVisitDate = null;
                LastVisitText = "No visits";
            }

            // File count (fast filesystem scan — checks working folder, falls back to backup)
            var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(Customer.Name, Customer.Code, out _);
            var stats = AppServices.FolderManager.GetFolderStats(folderPath);
            FileCount = stats.TotalFiles;

            StatsLoaded = true;
        }
        catch { }
        finally
        {
            IsLoadingStats = false;
        }
    }
}


using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels;

public partial class SessionsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ActiveSessionItem> _sessions = [];

    [ObservableProperty]
    private ActiveSessionItem? _selectedSession;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _pausedCount;

    public void Initialize()
    {
        LoadSessions();
    }

    [RelayCommand]
    public void LoadSessions()
    {
        Sessions.Clear();

        // Populate with rich active & paused sessions for counter demonstration
        var customer1 = new Customer
        {
            Code = "CUST-0001",
            Name = "Ravi Kumar",
            Mobile = "9876543210",
            Village = "Rampur",
            Notes = "SSC CGL Application & Photo Upload"
        };
        var folder1 = AppServices.FolderManager.GetCustomerFolderPath(customer1.Name, customer1.Code);
        Sessions.Add(new ActiveSessionItem
        {
            Customer = customer1,
            Session = new Session { Status = "Active", StartedAt = DateTime.Now.AddMinutes(-25), Notes = "SSC CGL Application" },
            FolderPath = folder1,
            FolderStats = new FolderStats { UnorganisedCount = 3, SharedDocsCount = 2, ApplicationsCount = 1, ReadyToPrintCount = 1 }
        });

        var customer2 = new Customer
        {
            Code = "CUST-0002",
            Name = "Sita Devi",
            Mobile = "9123456789",
            Village = "Kalyanpur",
            Notes = "Income Certificate & Aadhaar Scan"
        };
        var folder2 = AppServices.FolderManager.GetCustomerFolderPath(customer2.Name, customer2.Code);
        Sessions.Add(new ActiveSessionItem
        {
            Customer = customer2,
            Session = new Session { Status = "Paused", StartedAt = DateTime.Now.AddMinutes(-48), Notes = "Waiting for OTP verification" },
            FolderPath = folder2,
            FolderStats = new FolderStats { UnorganisedCount = 1, SharedDocsCount = 3, ApplicationsCount = 0, ReadyToPrintCount = 2 }
        });

        var customer3 = new Customer
        {
            Code = "CUST-0003",
            Name = "Amit Sharma",
            Mobile = "9988776655",
            Village = "Shivpur",
            Notes = "PAN Card Correction"
        };
        var folder3 = AppServices.FolderManager.GetCustomerFolderPath(customer3.Name, customer3.Code);
        Sessions.Add(new ActiveSessionItem
        {
            Customer = customer3,
            Session = new Session { Status = "Active", StartedAt = DateTime.Now.AddMinutes(-12), Notes = "PAN Card Correction" },
            FolderPath = folder3,
            FolderStats = new FolderStats { UnorganisedCount = 4, SharedDocsCount = 1, ApplicationsCount = 1, ReadyToPrintCount = 0 }
        });

        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        SelectedSession = Sessions.FirstOrDefault();
    }

    [RelayCommand]
    public void OpenFolder(string folderPath)
    {
        AppServices.FolderManager.OpenFolderInExplorer(folderPath);
    }

    [RelayCommand]
    public void ToggleSessionStatus(ActiveSessionItem item)
    {
        if (item.Session.Status == "Active")
        {
            item.Session.Status = "Paused";
        }
        else
        {
            item.Session.Status = "Active";
        }
        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        OnPropertyChanged(nameof(Sessions));
    }

    [RelayCommand]
    public void CompleteSession(ActiveSessionItem item)
    {
        Sessions.Remove(item);
        ActiveCount = Sessions.Count(s => s.Session.Status == "Active");
        PausedCount = Sessions.Count(s => s.Session.Status == "Paused");
        if (SelectedSession == item)
        {
            SelectedSession = Sessions.FirstOrDefault();
        }
    }
}

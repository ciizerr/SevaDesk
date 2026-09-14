using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using Windows.System;

namespace SevaDesk_App.ViewModels;

public partial class ApplicationsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ApplicationItem> _applications = [];

    [ObservableProperty]
    private ObservableCollection<ApplicationChecklistItem> _selectedChecklist = [];

    [ObservableProperty]
    private ApplicationItem? _selectedApplication;

    [ObservableProperty]
    private string _filterStatus = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ApplicationsViewModel()
    {
        LoadApplications();
    }

    [RelayCommand]
    public void LoadApplications()
    {
        Applications.Clear();

        Applications.Add(new ApplicationItem
        {
            Title = "SSC CGL 2026 Examination",
            CustomerName = "Ravi Kumar",
            PortalName = "ssc.gov.in",
            ApplicationNumber = "SSC-2026-94821",
            Status = "Docs Uploaded",
            ServiceCharge = 100,
            GovtFee = 100,
            RequiredDocs = "Passport Photo (20-50KB), Signature (10-20KB), 10th Marksheet, Aadhaar",
            CreatedAt = DateTime.Now.AddDays(-1)
        });

        Applications.Add(new ApplicationItem
        {
            Title = "PAN Card New Application (Form 49A)",
            CustomerName = "Sita Devi",
            PortalName = "onlineservices.nsdl.com",
            ApplicationNumber = "NSDL-PAN-88129",
            Status = "Draft",
            ServiceCharge = 120,
            GovtFee = 107,
            RequiredDocs = "Aadhaar Card, Passport Photo (Color), Physical Signature",
            CreatedAt = DateTime.Now.AddHours(-3)
        });

        Applications.Add(new ApplicationItem
        {
            Title = "UP Post-Matric Scholarship 2026",
            CustomerName = "Amit Sharma",
            PortalName = "scholarship.up.gov.in",
            ApplicationNumber = "SCH-UP-34190",
            Status = "Submitted",
            ServiceCharge = 150,
            GovtFee = 0,
            RequiredDocs = "Income Certificate, Caste Certificate, Fee Receipt, Bank Passbook",
            CreatedAt = DateTime.Now.AddDays(-3)
        });

        Applications.Add(new ApplicationItem
        {
            Title = "PM Kisan Samman Nidhi eKYC",
            CustomerName = "Ramesh Verma",
            PortalName = "pmkisan.gov.in",
            ApplicationNumber = "PMK-77182",
            Status = "Completed",
            ServiceCharge = 50,
            GovtFee = 0,
            RequiredDocs = "Aadhaar OTP / Biometric, Land Registration Proof",
            CreatedAt = DateTime.Now.AddDays(-5)
        });

        SelectedApplication = Applications.FirstOrDefault();
        UpdateChecklistForSelected();
    }

    partial void OnSelectedApplicationChanged(ApplicationItem? value)
    {
        UpdateChecklistForSelected();
    }

    private void UpdateChecklistForSelected()
    {
        SelectedChecklist.Clear();
        if (SelectedApplication == null) return;

        var docs = SelectedApplication.RequiredDocs.Split(',', StringSplitOptions.TrimEntries);
        foreach (var doc in docs)
        {
            SelectedChecklist.Add(new ApplicationChecklistItem
            {
                Title = doc,
                IsCompleted = SelectedApplication.Status != "Draft",
                Category = "Document"
            });
        }
    }

    [RelayCommand]
    public async Task OpenPortalUrl(string portal)
    {
        if (string.IsNullOrWhiteSpace(portal)) return;
        var url = portal.StartsWith("http") ? portal : $"https://{portal}";
        try
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
        catch
        {
            StatusMessage = $"Failed to launch {url}";
        }
    }

    [RelayCommand]
    public void UpdateApplicationStatus(string newStatus)
    {
        if (SelectedApplication == null) return;
        SelectedApplication.Status = newStatus;
        StatusMessage = $"Status updated to '{newStatus}' for {SelectedApplication.Title}.";
        OnPropertyChanged(nameof(Applications));
    }
}

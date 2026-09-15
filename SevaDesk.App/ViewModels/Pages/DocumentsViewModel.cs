using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class DocumentsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DocumentItem> _pendingDocuments = [];

    [ObservableProperty]
    private ObservableCollection<DocumentItem> _processedDocuments = [];

    [ObservableProperty]
    private ObservableCollection<CompressionPreset> _compressionPresets = [];

    [ObservableProperty]
    private DocumentItem? _selectedDocument;

    [ObservableProperty]
    private int _unorganisedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public DocumentsViewModel()
    {
        InitializePresets();
        LoadDocuments();
    }

    private void InitializePresets()
    {
        CompressionPresets.Clear();
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "ssc_photo",
            Name = "Passport Photo (SSC/UPSC)",
            TargetSize = "20 KB – 50 KB",
            Description = "Dimensions 3.5cm x 4.5cm, JPEG format",
            IconGlyph = "\uEB9F"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "govt_sign",
            Name = "Applicant Signature",
            TargetSize = "10 KB – 20 KB",
            Description = "Dimensions 4.0cm x 2.0cm, clear black ink",
            IconGlyph = "\uEDC6"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "pdf_doc",
            Name = "Govt Portal PDF Upload",
            TargetSize = "< 300 KB",
            Description = "High legibility grayscale/color A4 PDF",
            IconGlyph = "\uE8A5"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "pan_doc",
            Name = "PAN / NSDL Format",
            TargetSize = "< 2 MB (200 DPI)",
            Description = "Color scan for Form 49A supporting docs",
            IconGlyph = "\uE749"
        });
    }

    [RelayCommand]
    public void LoadDocuments()
    {
        PendingDocuments.Clear();
        ProcessedDocuments.Clear();

        // Sample pending documents arriving at the counter
        PendingDocuments.Add(new DocumentItem
        {
            Name = "WhatsApp Image 2026-09-14 at 11.23.jpeg",
            CustomerName = "Ravi Kumar (CUST-0001)",
            Category = "00_Unorganised",
            FileSize = "2.4 MB",
            Extension = ".jpg",
            Status = "Needs Compression",
            Glyph = "\uEB9F",
            CreatedAt = DateTime.Now.AddMinutes(-15)
        });

        PendingDocuments.Add(new DocumentItem
        {
            Name = "Scanned_Aadhaar_Front_Back.pdf",
            CustomerName = "Ravi Kumar (CUST-0001)",
            Category = "00_Unorganised",
            FileSize = "1.8 MB",
            Extension = ".pdf",
            Status = "Ready to Tag",
            Glyph = "\uE8A5",
            CreatedAt = DateTime.Now.AddMinutes(-12)
        });

        PendingDocuments.Add(new DocumentItem
        {
            Name = "IMG_20260914_120411_Signature.png",
            CustomerName = "Amit Sharma (CUST-0003)",
            Category = "00_Unorganised",
            FileSize = "850 KB",
            Extension = ".png",
            Status = "Needs Resize (10-20KB)",
            Glyph = "\uEDC6",
            CreatedAt = DateTime.Now.AddMinutes(-8)
        });

        PendingDocuments.Add(new DocumentItem
        {
            Name = "Caste_Certificate_Patwari.pdf",
            CustomerName = "Sita Devi (CUST-0002)",
            Category = "00_Unorganised",
            FileSize = "420 KB",
            Extension = ".pdf",
            Status = "Needs Compression (<300KB)",
            Glyph = "\uE8A5",
            CreatedAt = DateTime.Now.AddMinutes(-5)
        });

        // Sample processed documents
        ProcessedDocuments.Add(new DocumentItem
        {
            Name = "Ravi_Kumar_Passport_Photo_45kb.jpg",
            CustomerName = "Ravi Kumar (CUST-0001)",
            Category = "02_Applications",
            FileSize = "42 KB",
            Extension = ".jpg",
            Status = "Compressed",
            Glyph = "\uE73E",
            CreatedAt = DateTime.Now.AddMinutes(-30)
        });

        ProcessedDocuments.Add(new DocumentItem
        {
            Name = "AdmitCard_SSC_CGL_Tier1.pdf",
            CustomerName = "Amit Sharma (CUST-0003)",
            Category = "03_Ready to Print",
            FileSize = "180 KB",
            Extension = ".pdf",
            Status = "Queued for Print",
            Glyph = "\uE749",
            CreatedAt = DateTime.Now.AddMinutes(-20)
        });

        UnorganisedCount = PendingDocuments.Count;
        SelectedDocument = PendingDocuments.FirstOrDefault();
    }

    [RelayCommand]
    public void MoveDocument(string targetCategory)
    {
        if (SelectedDocument == null) return;

        var doc = SelectedDocument;
        PendingDocuments.Remove(doc);
        doc.Category = targetCategory;
        doc.Status = $"Moved to {targetCategory}";
        ProcessedDocuments.Insert(0, doc);

        UnorganisedCount = PendingDocuments.Count;
        SelectedDocument = PendingDocuments.FirstOrDefault();
        StatusMessage = $"'{doc.Name}' moved to {targetCategory}.";
    }

    [RelayCommand]
    public void ApplyPreset(CompressionPreset preset)
    {
        if (SelectedDocument == null) return;

        StatusMessage = $"Applying '{preset.Name}' to {SelectedDocument.Name} (Target: {preset.TargetSize})...";
        SelectedDocument.Status = $"Compressed to {preset.TargetSize}";
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        var basePath = AppServices.FolderManager.BaseDirectory;
        AppServices.FolderManager.OpenFolderInExplorer(basePath);
    }
}

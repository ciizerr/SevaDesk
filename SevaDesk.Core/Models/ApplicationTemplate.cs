using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class ApplicationTemplate : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _category = "Jobs & Exams";

    [ObservableProperty]
    private string _portalUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultTotalFee))]
    [NotifyPropertyChangedFor(nameof(FormattedServiceFee))]
    [NotifyPropertyChangedFor(nameof(FormattedTotalFee))]
    [NotifyPropertyChangedFor(nameof(DefaultServiceFeeDouble))]
    private decimal _defaultServiceFee = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultTotalFee))]
    [NotifyPropertyChangedFor(nameof(FormattedGovtFee))]
    [NotifyPropertyChangedFor(nameof(FormattedTotalFee))]
    [NotifyPropertyChangedFor(nameof(DefaultGovtFeeDouble))]
    private decimal _defaultGovtFee = 0;

    public decimal DefaultTotalFee => DefaultServiceFee + DefaultGovtFee;
    public string FormattedServiceFee => $"₹{DefaultServiceFee:N0}";
    public string FormattedGovtFee => $"₹{DefaultGovtFee:N0}";
    public string FormattedTotalFee => $"₹{DefaultTotalFee:N0}";

    public double DefaultServiceFeeDouble
    {
        get => (double)DefaultServiceFee;
        set => DefaultServiceFee = (decimal)value;
    }

    public double DefaultGovtFeeDouble
    {
        get => (double)DefaultGovtFee;
        set => DefaultGovtFee = (decimal)value;
    }

    [ObservableProperty]
    private string _requiredDocs = "Photo, Signature, Aadhaar";

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class ManageRatesDialog : ContentDialog
{
    private readonly ObservableCollection<ServiceRateItem> _allRates = [];
    private readonly ObservableCollection<ServiceRateItem> _displayedRates = [];

    private string? _editingServiceId;
    private string _selectedGlyph = "\uE749";
    private readonly List<Button> _iconButtons = [];

    private readonly (string Glyph, string Tooltip)[] _availableGlyphs =
    [
        ("\uE749", "Print / Xerox"),
        ("\uE790", "Color / Photo"),
        ("\uE8A5", "Document / Scan"),
        ("\uE8C7", "PVC Card / ID"),
        ("\uE77B", "Govt / Online Form"),
        ("\uE8C1", "Affidavit / Typing"),
        ("\uE7C3", "Lamination / Stamp"),
        ("\uE7F4", "Desktop / Computer"),
        ("\uE896", "Download / Online"),
        ("\uE8F1", "General Service")
    ];

    private readonly string[] _presetCategories =
    [
        "Printing",
        "Scanning",
        "Finishing",
        "Cards",
        "Services",
        "Blank Forms",
        "Affidavits",
        "Computer / Online"
    ];

    private readonly string[] _presetUnits =
    [
        "page",
        "copy",
        "doc",
        "card",
        "sheet",
        "application",
        "form",
        "photo",
        "hour",
        "item"
    ];

    public ManageRatesDialog()
    {
        InitializeComponent();
        this.EnableLightDismiss();
        ServicesListView.ItemsSource = _displayedRates;
        InitializeFormFields();
        _ = LoadRatesDataAsync();
    }

    private void InitializeFormFields()
    {
        CmbCategory.ItemsSource = _presetCategories;
        if (_presetCategories.Length > 0) CmbCategory.SelectedIndex = 0;

        CmbUnit.ItemsSource = _presetUnits;
        if (_presetUnits.Length > 0) CmbUnit.SelectedIndex = 0;

        // Build 5x2 grid of icon buttons
        IconPickerGrid.Children.Clear();
        _iconButtons.Clear();

        for (int i = 0; i < _availableGlyphs.Length; i++)
        {
            var (glyph, tooltip) = _availableGlyphs[i];
            var btn = new Button
            {
                Content = new FontIcon { Glyph = glyph, FontSize = 13 },
                Tag = glyph,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Height = 32
            };
            ToolTipService.SetToolTip(btn, tooltip);

            int col = i % 5;
            int row = i / 5;
            Grid.SetColumn(btn, col);
            Grid.SetRow(btn, row);

            btn.Click += (s, e) =>
            {
                if (btn.Tag is string g)
                {
                    SelectGlyph(g);
                }
            };

            _iconButtons.Add(btn);
            IconPickerGrid.Children.Add(btn);
        }

        SelectGlyph(_selectedGlyph);
    }

    private void SelectGlyph(string glyph)
    {
        _selectedGlyph = glyph;
        foreach (var btn in _iconButtons)
        {
            bool isSelected = string.Equals(btn.Tag as string, glyph, StringComparison.Ordinal);
            btn.Style = isSelected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
        }
    }

    private async Task LoadRatesDataAsync()
    {
        _allRates.Clear();
        var rates = await AppServices.ServiceRates.GetAllActiveRatesAsync();
        foreach (var r in rates)
        {
            _allRates.Add(r);
        }
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var q = SearchRateBox.Text?.Trim() ?? string.Empty;
        _displayedRates.Clear();

        foreach (var rate in _allRates)
        {
            if (string.IsNullOrWhiteSpace(q) ||
                rate.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                rate.Category.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                _displayedRates.Add(rate);
            }
        }
    }

    private void SearchRateBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private void ServicesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServicesListView.SelectedItem is ServiceRateItem item)
        {
            LoadIntoEditForm(item);
        }
    }

    private void LoadIntoEditForm(ServiceRateItem item)
    {
        _editingServiceId = item.Id;
        TxtFormHeader.Text = "Edit Service";
        TxtFormSubtitle.Text = $"Updating '{item.ServiceName}'";
        BtnSaveService.Content = "Update Rate";

        TxtServiceName.Text = item.ServiceName;
        CmbCategory.Text = item.Category;
        NumRate.Value = (double)item.Rate;
        CmbUnit.Text = item.Unit;

        SelectGlyph(item.Glyph);
        DialogStatusInfoBar.IsOpen = false;
    }

    private void ResetForm()
    {
        _editingServiceId = null;
        TxtFormHeader.Text = "Add New Service";
        TxtFormSubtitle.Text = "Configure pricing, category, and unit";
        BtnSaveService.Content = "Save Service";

        TxtServiceName.Text = string.Empty;
        if (_presetCategories.Length > 0) CmbCategory.SelectedIndex = 0;
        NumRate.Value = 10;
        if (_presetUnits.Length > 0) CmbUnit.SelectedIndex = 0;
        SelectGlyph(_availableGlyphs[0].Glyph);

        ServicesListView.SelectedItem = null;
        DialogStatusInfoBar.IsOpen = false;
    }

    private void NewServiceButton_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
        TxtServiceName.Focus(FocusState.Programmatic);
    }

    private void EditService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServiceRateItem item)
        {
            ServicesListView.SelectedItem = item;
            LoadIntoEditForm(item);
        }
    }

    private async void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServiceRateItem item)
        {
            try
            {
                await AppServices.ServiceRates.DeleteRateAsync(item.Id);
                _allRates.Remove(item);
                _displayedRates.Remove(item);

                if (_editingServiceId == item.Id)
                {
                    ResetForm();
                }

                ShowStatus($"\"{item.ServiceName}\" deleted successfully.", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error deleting service: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void SaveService_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtServiceName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("Please enter a valid service name.", InfoBarSeverity.Warning);
            TxtServiceName.Focus(FocusState.Programmatic);
            return;
        }

        var rateVal = (decimal)NumRate.Value;
        if (rateVal < 0)
        {
            ShowStatus("Service rate cannot be negative.", InfoBarSeverity.Warning);
            return;
        }

        var category = string.IsNullOrWhiteSpace(CmbCategory.Text) ? "General" : CmbCategory.Text.Trim();
        var unit = string.IsNullOrWhiteSpace(CmbUnit.Text) ? "page" : CmbUnit.Text.Trim();

        try
        {
            if (_editingServiceId != null)
            {
                // Update existing service
                var existing = _allRates.FirstOrDefault(r => r.Id == _editingServiceId);
                if (existing != null)
                {
                    existing.ServiceName = name;
                    existing.Rate = rateVal;
                    existing.Category = category;
                    existing.Unit = unit;
                    existing.Glyph = _selectedGlyph;

                    await AppServices.ServiceRates.UpdateRateAsync(existing);
                    ShowStatus($"Rate for \"{name}\" updated successfully.", InfoBarSeverity.Success);
                }
            }
            else
            {
                // Create new service
                var newRate = new ServiceRateItem
                {
                    Id = Guid.NewGuid().ToString(),
                    ServiceName = name,
                    Rate = rateVal,
                    Category = category,
                    Unit = unit,
                    Glyph = _selectedGlyph,
                    IsActive = true
                };

                await AppServices.ServiceRates.AddRateAsync(newRate);
                _allRates.Add(newRate);
                ApplySearchFilter();
                ShowStatus($"New service \"{name}\" added to rate card.", InfoBarSeverity.Success);
            }

            ResetForm();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error saving service: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        DialogStatusInfoBar.Message = message;
        DialogStatusInfoBar.Severity = severity;
        DialogStatusInfoBar.IsOpen = true;
    }
}

using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class LinkApplicationDialog : ContentDialog
{
    private readonly string _customerId;
    private readonly string _customerName;
    private readonly string? _sessionId;

    public ApplicationTemplate? SelectedTemplate { get; private set; }
    public string AppNumberText => TxtAppNumber.Text.Trim();
    public string NotesText => TxtNotes.Text.Trim();
    public bool IsConfirmed { get; private set; }

    public LinkApplicationDialog(string customerName, string customerId, string? sessionId = null)
    {
        InitializeComponent();
        this.EnableLightDismiss();
        _customerName = customerName;
        _customerId = customerId;
        _sessionId = sessionId;
        TxtCustomerBanner.Text = $"Applying for: {customerName}";

        PrimaryButtonClick += LinkApplicationDialog_PrimaryButtonClick;
    }

    private void LinkApplicationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (SelectedTemplate == null && string.IsNullOrWhiteSpace(SearchSuggestBox.Text))
        {
            args.Cancel = true;
            SearchSuggestBox.Header = "Search Scheme / Form Catalog * (Select or Type Scheme)";
        }
        else
        {
            IsConfirmed = true;
        }
    }

    private async void SearchSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                MatchedTemplateCard.Visibility = Visibility.Collapsed;
                NotFoundCard.Visibility = Visibility.Collapsed;
                SelectedTemplate = null;
                return;
            }

            var matches = (await AppServices.Applications.SearchTemplatesAsync(query)).ToList();
            sender.ItemsSource = matches;

            if (matches.Count == 0)
            {
                MatchedTemplateCard.Visibility = Visibility.Collapsed;
                NotFoundCard.Visibility = Visibility.Visible;
                TxtNotFoundPrompt.Text = $"Scheme '{query}' is not in your catalog.";
                SelectedTemplate = null;
            }
            else
            {
                NotFoundCard.Visibility = Visibility.Collapsed;
                // If exact title match
                var exact = matches.FirstOrDefault(m => string.Equals(m.Title, query, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    ApplyTemplate(exact);
                }
            }
        }
    }

    private void SearchSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ApplicationTemplate tmpl)
        {
            ApplyTemplate(tmpl);
        }
    }

    private void SearchSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is ApplicationTemplate tmpl)
        {
            ApplyTemplate(tmpl);
        }
        else if (!string.IsNullOrWhiteSpace(sender.Text))
        {
            // If already matched, keep it; otherwise prompt quick-add
            if (SelectedTemplate == null)
            {
                NotFoundCard.Visibility = Visibility.Visible;
                TxtNotFoundPrompt.Text = $"Scheme '{sender.Text}' is not in your catalog.";
            }
        }
    }

    private void ApplyTemplate(ApplicationTemplate tmpl)
    {
        SelectedTemplate = tmpl;
        MatchedTemplateCard.Visibility = Visibility.Visible;
        NotFoundCard.Visibility = Visibility.Collapsed;

        TxtMatchedTitle.Text = tmpl.Title;
        TxtMatchedPortal.Text = tmpl.PortalUrl;
        TxtMatchedFees.Text = $"Service: {tmpl.FormattedServiceFee} | Govt: {tmpl.FormattedGovtFee}";
        TxtMatchedDocs.Text = string.IsNullOrWhiteSpace(tmpl.RequiredDocs) ? "Photo, Signature, Aadhaar" : tmpl.RequiredDocs;
    }

    private async void QuickAddFallback_Click(object sender, RoutedEventArgs e)
    {
        var typedName = SearchSuggestBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(typedName)) return;

        // Auto-create draft template in catalog for later completion
        var draftTemplate = new ApplicationTemplate
        {
            Title = typedName,
            Category = "Jobs & Exams",
            PortalUrl = "https://",
            DefaultServiceFee = 100,
            DefaultGovtFee = 0,
            RequiredDocs = "Photo, Signature, Aadhaar",
            Notes = "Quick-linked from Counter Session"
        };
        await AppServices.Applications.CreateTemplateAsync(draftTemplate);

        ApplyTemplate(draftTemplate);
        IsConfirmed = true;
        Hide();
    }

    public ApplicationItem BuildApplication()
    {
        if (SelectedTemplate != null)
        {
            return new ApplicationItem
            {
                CustomerId = _customerId,
                SessionId = _sessionId,
                CustomerName = _customerName,
                Title = SelectedTemplate.Title,
                PortalName = SelectedTemplate.PortalUrl,
                ApplicationNumber = string.IsNullOrWhiteSpace(AppNumberText) ? $"REG-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Millisecond}" : AppNumberText,
                Status = "Draft",
                ServiceCharge = SelectedTemplate.DefaultServiceFee,
                GovtFee = SelectedTemplate.DefaultGovtFee,
                RequiredDocs = SelectedTemplate.RequiredDocs,
                Notes = string.IsNullOrWhiteSpace(NotesText) ? SelectedTemplate.Notes : NotesText,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        var fallbackTitle = string.IsNullOrWhiteSpace(SearchSuggestBox.Text) ? "Online Form" : SearchSuggestBox.Text.Trim();
        return new ApplicationItem
        {
            CustomerId = _customerId,
            SessionId = _sessionId,
            CustomerName = _customerName,
            Title = fallbackTitle,
            PortalName = "gov.in",
            ApplicationNumber = string.IsNullOrWhiteSpace(AppNumberText) ? $"REG-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Millisecond}" : AppNumberText,
            Status = "Draft",
            ServiceCharge = 100,
            GovtFee = 0,
            RequiredDocs = "Photo, Signature, Aadhaar",
            Notes = NotesText,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

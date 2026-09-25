using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class NewCustomerDialog : ContentDialog
{
    private CancellationTokenSource? _searchCts;

    public Customer? SelectedExistingCustomer { get; private set; }
    public string CustomerName => NameSuggestBox.Text?.Trim() ?? string.Empty;
    public string Mobile => MobileBox.Text?.Trim() ?? string.Empty;
    public string Village => VillageBox.Text?.Trim() ?? string.Empty;
    public string IdRef => IdRefBox.Text?.Trim() ?? string.Empty;
    public string Notes => NotesBox.Text?.Trim() ?? string.Empty;

    public NewCustomerDialog()
    {
        InitializeComponent();
        this.EnableLightDismiss(() => string.IsNullOrWhiteSpace(CustomerName) && string.IsNullOrWhiteSpace(Mobile) && string.IsNullOrWhiteSpace(Notes));
        Closing += OnDialogClosing;
    }

    private async void NameSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var text = sender.Text?.Trim();

            if (SelectedExistingCustomer != null && !string.Equals(SelectedExistingCustomer.Name, text, StringComparison.OrdinalIgnoreCase))
            {
                SelectedExistingCustomer = null;
                ExistingCustomerBadge.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                sender.ItemsSource = null;
                return;
            }

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                var results = await AppServices.Customers.SearchAsync(text);
                if (!token.IsCancellationRequested)
                {
                    sender.ItemsSource = results.ToList();
                }
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    private void NameSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Customer customer)
        {
            SelectCustomer(customer);
        }
    }

    private async void NameSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is Customer customer)
        {
            SelectCustomer(customer);
        }
        else if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            var text = args.QueryText.Trim();
            var matches = (await AppServices.Customers.SearchAsync(text)).ToList();
            var exact = matches.FirstOrDefault(c => string.Equals(c.Name, text, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                SelectCustomer(exact);
            }
        }
    }

    private void SelectCustomer(Customer customer)
    {
        SelectedExistingCustomer = customer;
        NameSuggestBox.Text = customer.Name;
        MobileBox.Text = customer.Mobile ?? string.Empty;
        VillageBox.Text = customer.Village ?? string.Empty;
        IdRefBox.Text = customer.IdReference ?? string.Empty;

        var template = AppServices.Localization.GetString("Dialog.NewCustomer.ExistingCustomerFound");
        if (string.IsNullOrWhiteSpace(template) || template == "Dialog.NewCustomer.ExistingCustomerFound")
        {
            template = "Existing customer matched ({0}) — session will link to profile.";
        }
        TxtBadgeInfo.Text = string.Format(template, customer.Code);
        ExistingCustomerBadge.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void OnDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (args.Result == ContentDialogResult.Primary && string.IsNullOrWhiteSpace(CustomerName))
        {
            args.Cancel = true;
            NameSuggestBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }
}

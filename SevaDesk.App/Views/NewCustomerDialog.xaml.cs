using Microsoft.UI.Xaml.Controls;

namespace SevaDesk_App.Views;

public sealed partial class NewCustomerDialog : ContentDialog
{
    public string CustomerName => NameBox.Text.Trim();
    public string Mobile => MobileBox.Text.Trim();
    public string Village => VillageBox.Text.Trim();
    public string IdRef => IdRefBox.Text.Trim();
    public string Notes => NotesBox.Text.Trim();

    public NewCustomerDialog()
    {
        InitializeComponent();
        Closing += OnDialogClosing;
    }

    private void OnDialogClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (args.Result == ContentDialogResult.Primary && string.IsNullOrWhiteSpace(CustomerName))
        {
            args.Cancel = true;
            NameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
    }
}

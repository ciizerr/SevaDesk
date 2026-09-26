using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class CompleteSessionDialog : ContentDialog
{
    private readonly Customer _customer;

    public string Mobile => TxtMobile.Text?.Trim() ?? string.Empty;
    public string IdReference => TxtIdRef.Text?.Trim() ?? string.Empty;
    public string Village => TxtVillage.Text?.Trim() ?? string.Empty;

    public CompleteSessionDialog(Customer customer)
    {
        _customer = customer;
        InitializeComponent();
        this.EnableLightDismiss();

        var template = AppServices.Localization.GetString("Dialog.CompleteSession.Subtitle");
        if (string.IsNullOrWhiteSpace(template) || template == "Dialog.CompleteSession.Subtitle")
        {
            template = "Save details for {0} for future visits:";
        }
        TxtPromptSubtitle.Text = string.Format(template, customer.Name);

        TxtMobile.Text = customer.Mobile ?? string.Empty;
        TxtIdRef.Text = customer.IdReference ?? string.Empty;
        TxtVillage.Text = customer.Village ?? string.Empty;
    }

    public void ApplyToCustomer(Customer customer)
    {
        if (!string.IsNullOrWhiteSpace(Mobile))
            customer.Mobile = Mobile;
        if (!string.IsNullOrWhiteSpace(IdReference))
            customer.IdReference = IdReference;
        if (!string.IsNullOrWhiteSpace(Village))
            customer.Village = Village;
    }
}

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SevaDesk_App.Services;

namespace SevaDesk_App.Views.Dialogs;

public sealed partial class OnboardingDialog : ContentDialog
{
    private int _currentStep = 1;
    private const int TotalSteps = 5;
    private bool _isMobileValid = true;

    public OnboardingDialog()
    {
        InitializeComponent();
        this.EnableLightDismiss(() => false); // Don't light-dismiss first-run wizard by mistake

        LoadExistingSettings();
        ConfigureInputs();
        UpdateStepView();
        UpdateLanguageSelectionUi();
    }

    private void LoadExistingSettings()
    {
        var shopName = AppServices.Database.GetSetting("shop_name", "");
        var operatorName = AppServices.Database.GetSetting("operator_name", "");
        var contact = AppServices.Database.GetSetting("contact_number", "");
        var address = AppServices.Database.GetSetting("shop_address", "");
        var upi = AppServices.Database.GetSetting("shop_upi_vpa")
                  ?? AppServices.Database.GetSetting("shop_upi_id", "");
        var payee = AppServices.Database.GetSetting("payee_name", "");

        BoxShopName.Text = shopName;
        BoxOperatorName.Text = operatorName;
        BoxContactNumber.Text = contact;
        BoxShopAddress.Text = address;
        BoxUpiId.Text = upi;
        BoxPayeeName.Text = string.IsNullOrWhiteSpace(payee) ? "SevaDesk Cyber Center" : payee;

        var defaultDocFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defWorking = System.IO.Path.Combine(defaultDocFolder, "SevaDesk");
        var defBackup = System.IO.Path.Combine(defaultDocFolder, "SevaDeskBackups");
        
        BoxWorkingDirectory.Text = AppServices.Database.GetSetting("working_directory", defWorking);
        BoxBackupDirectory.Text = AppServices.Database.GetSetting("backup_directory", defBackup);
    }

    private void ConfigureInputs()
    {
        BoxContactNumber.ConfigureNumericMobileInput(isValid =>
        {
            _isMobileValid = isValid;
            TxtContactError.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
        });
    }

    private void UpdateStepView()
    {
        // Toggle step panels
        Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        Step5Panel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;

        // Step indicator text
        var stepFmt = AppServices.Localization.GetString("Onboarding.StepIndicator", "Step {0} of {1}");
        TxtStepIndicator.Text = string.Format(stepFmt, _currentStep, TotalSteps);

        // Progress Dots
        var accentBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var mutedBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];

        Dot1.Background = _currentStep >= 1 ? accentBrush : mutedBrush;
        Dot2.Background = _currentStep >= 2 ? accentBrush : mutedBrush;
        Dot3.Background = _currentStep >= 3 ? accentBrush : mutedBrush;
        Dot4.Background = _currentStep >= 4 ? accentBrush : mutedBrush;
        Dot5.Background = _currentStep >= 5 ? accentBrush : mutedBrush;

        // Navigation Buttons
        BtnBack.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (_currentStep == TotalSteps)
        {
            BtnNext.Content = AppServices.Localization.GetString("Onboarding.FinishBtn", "Start Using SevaDesk");
        }
        else
        {
            BtnNext.Content = AppServices.Localization.GetString("Onboarding.NextBtn", "Next");
        }

        BtnSkip.Content = AppServices.Localization.GetString("Onboarding.SkipBtn", "Skip for now");
        BtnBack.Content = AppServices.Localization.GetString("Onboarding.BackBtn", "Back");

        // Refresh dynamic UI strings for current language
        RefreshLocalizedStrings();

        // Step-specific updates
        if (_currentStep == 3)
        {
            GenerateQrPreview();
        }
    }

    private void RefreshLocalizedStrings()
    {
        // Step 1
        TxtStep1Title.Text = AppServices.Localization.GetString("Onboarding.Step1.Title", "Welcome to SevaDesk");
        TxtStep1Tagline.Text = AppServices.Localization.GetString("Onboarding.Step1.Tagline", "Your smart companion for Cyber Cafés & Digital Seva / CSC Centers");
        TxtStep1LangPrompt.Text = AppServices.Localization.GetString("Onboarding.Step1.LanguageTitle", "Select Your Language");
        TxtStep1LangSub.Text = AppServices.Localization.GetString("Onboarding.Step1.LanguageSubtitle", "You can change this anytime from Settings");

        // Step 2
        TxtStep2Title.Text = AppServices.Localization.GetString("Onboarding.Step2.Title", "Shop & Operator Profile");
        TxtStep2Subtitle.Text = AppServices.Localization.GetString("Onboarding.Step2.Subtitle", "This information appears on customer receipts and invoices");
        RunShopNameLabel.Text = AppServices.Localization.GetString("Onboarding.Step2.ShopName", "Shop / Center Name");
        BoxShopName.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step2.ShopNamePlaceholder", "e.g. Shri Krishna Cyber Cafe / Digital Seva Kendra");
        BoxOperatorName.Header = AppServices.Localization.GetString("Onboarding.Step2.OperatorName", "Operator / VLE Name");
        BoxOperatorName.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step2.OperatorNamePlaceholder", "e.g. Ramesh Kumar");
        BoxContactNumber.Header = AppServices.Localization.GetString("Onboarding.Step2.ContactNumber", "WhatsApp / Mobile Number");
        BoxContactNumber.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step2.ContactPlaceholder", "10-digit mobile number");
        BoxShopAddress.Header = AppServices.Localization.GetString("Onboarding.Step2.Address", "Shop Address / Area");
        BoxShopAddress.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step2.AddressPlaceholder", "e.g. Main Market, Near Bus Stand");

        // Step 3
        TxtStep3Title.Text = AppServices.Localization.GetString("Onboarding.Step3.Title", "Digital Payments & UPI QR");
        TxtStep3Subtitle.Text = AppServices.Localization.GetString("Onboarding.Step3.Subtitle", "Show instant payment QR codes on counter and customer receipts");
        BoxUpiId.Header = AppServices.Localization.GetString("Onboarding.Step3.UpiId", "Shop UPI ID (VPA)");
        BoxUpiId.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step3.UpiIdPlaceholder", "e.g. shopname@okhdfcbank or 9876543210@paytm");
        BoxPayeeName.Header = AppServices.Localization.GetString("Onboarding.Step3.PayeeName", "Payee Name");
        BoxPayeeName.PlaceholderText = AppServices.Localization.GetString("Onboarding.Step3.PayeePlaceholder", "e.g. Digital Seva Kendra");
        TxtUpiHint.Text = AppServices.Localization.GetString("Onboarding.Step3.QrPreviewHint", "Customers can scan this to pay directly to your shop bank account via GPay, PhonePe, Paytm, or BHIM.");

        // Step 5
        TxtStep5Title.Text = AppServices.Localization.GetString("Onboarding.Step5.Title", "You're All Set!");
        TxtStep5Subtitle.Text = AppServices.Localization.GetString("Onboarding.Step5.Subtitle", "Everything is ready for your counter operations");
        TxtFeature1Title.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature1Title", "Desktop Floating Widget");
        TxtFeature1Desc.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature1Desc", "Docked on your screen edge to manage customer sessions without leaving browser or portals.");
        TxtFeature2Title.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature2Title", "Smart Auto-File Triage");
        TxtFeature2Desc.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature2Desc", "Detects incoming Bluetooth and WhatsApp files and moves them directly into active customer folders.");
        TxtFeature3Title.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature3Title", "Clean Invoices & WhatsApp Sharing");
        TxtFeature3Desc.Text = AppServices.Localization.GetString("Onboarding.Step5.Feature3Desc", "Generate professional receipts, A4 invoices, and share via WhatsApp in 1 click.");
    }

    private void UpdateLanguageSelectionUi()
    {
        var currentLang = AppServices.Localization.CurrentLanguageCode;
        bool isHindi = string.Equals(currentLang, "hi-IN", StringComparison.OrdinalIgnoreCase);

        var accentBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var strokeBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

        BtnLangEnglish.BorderBrush = isHindi ? strokeBrush : accentBrush;
        BtnLangEnglish.BorderThickness = new Thickness(isHindi ? 1 : 2);
        IconCheckEnglish.Visibility = isHindi ? Visibility.Collapsed : Visibility.Visible;

        BtnLangHindi.BorderBrush = isHindi ? accentBrush : strokeBrush;
        BtnLangHindi.BorderThickness = new Thickness(isHindi ? 2 : 1);
        IconCheckHindi.Visibility = isHindi ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LangEnglish_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Localization.SetLanguage("en-US");
        UpdateLanguageSelectionUi();
        UpdateStepView();
    }

    private void LangHindi_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Localization.SetLanguage("hi-IN");
        UpdateLanguageSelectionUi();
        UpdateStepView();
    }

    private void BoxShopName_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Live validation if needed
    }

    private void UpiInputs_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentStep == 3)
        {
            GenerateQrPreview();
        }
    }

    private void GenerateQrPreview()
    {
        var vpa = BoxUpiId.Text?.Trim();
        var payee = string.IsNullOrWhiteSpace(BoxPayeeName.Text) ? "My Shop" : BoxPayeeName.Text.Trim();

        if (string.IsNullOrWhiteSpace(vpa))
        {
            ImgQrPreview.Visibility = Visibility.Collapsed;
            TxtQrPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        ImgQrPreview.Visibility = Visibility.Visible;
        TxtQrPlaceholder.Visibility = Visibility.Collapsed;

        var payload = AppServices.QrCode.BuildUpiPayload(vpa, payee);
        var bmp = AppServices.QrCode.GenerateQrBitmap(payload, 8);
        if (bmp != null)
        {
            ImgQrPreview.Source = bmp;
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepView();
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2 && !_isMobileValid)
        {
            TxtContactError.Visibility = Visibility.Visible;
            return;
        }

        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepView();
        }
        else
        {
            SaveSettingsAndFinish();
        }
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Database.SetSetting("is_onboarded", "true");
        this.Hide();
    }

    private void SaveSettingsAndFinish()
    {
        var shopName = BoxShopName.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(shopName))
        {
            AppServices.Database.SetSetting("shop_name", shopName);
        }

        var operatorName = BoxOperatorName.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            AppServices.Database.SetSetting("operator_name", operatorName);
        }

        var contact = BoxContactNumber.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(contact))
        {
            AppServices.Database.SetSetting("contact_number", contact);
        }

        var address = BoxShopAddress.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            AppServices.Database.SetSetting("shop_address", address);
        }

        var upi = BoxUpiId.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(upi))
        {
            AppServices.Database.SetSetting("shop_upi_vpa", upi);
        }

        var payee = BoxPayeeName.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(payee))
        {
            AppServices.Database.SetSetting("payee_name", payee);
        }

        var workingDir = BoxWorkingDirectory.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(workingDir))
        {
            AppServices.Database.SetSetting("working_directory", workingDir);
        }

        var backupDir = BoxBackupDirectory.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(backupDir))
        {
            AppServices.Database.SetSetting("backup_directory", backupDir);
        }

        AppServices.Database.SetSetting("is_onboarded", "true");
        this.Hide();
    }

    private async void BtnBrowseWorkingDir_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            BoxWorkingDirectory.Text = folder.Path;
        }
    }

    private async void BtnBrowseBackupDir_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            BoxBackupDirectory.Text = folder.Path;
        }
    }
}

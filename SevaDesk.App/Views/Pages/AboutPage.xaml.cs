using Microsoft.UI.Xaml.Controls;
using SevaDesk_App.ViewModels.Pages;

namespace SevaDesk_App.Views.Pages;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; } = new();

    public AboutPage()
    {
        InitializeComponent();
    }
}

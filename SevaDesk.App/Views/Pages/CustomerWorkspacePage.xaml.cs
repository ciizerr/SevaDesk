using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SevaDesk.Core.Models;
using SevaDesk_App.ViewModels.Pages;

namespace SevaDesk_App.Views.Pages;

public sealed partial class CustomerWorkspacePage : Page
{
    public CustomerWorkspaceViewModel ViewModel { get; } = new();

    public CustomerWorkspacePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Customer customer)
        {
            await ViewModel.InitializeAsync(customer);
        }
    }

    public static Visibility EmptyListVisibility(int count)
    {
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.Instance?.GoBack();
    }
}

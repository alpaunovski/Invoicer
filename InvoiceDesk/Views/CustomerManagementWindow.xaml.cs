using System.Windows;
using InvoiceDesk.ViewModels;

namespace InvoiceDesk.Views;

public partial class CustomerManagementWindow : Window
{
    private readonly CustomerManagementViewModel _viewModel;

    public CustomerManagementWindow(CustomerManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

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
        // Ensure the country dropdown sees the collection even if XAML binding fails.
        CustomersGrid.ItemsSource = _viewModel.Customers;
        // Assign ItemsSource for the country column if it exists.
        foreach (var column in CustomersGrid.Columns.OfType<System.Windows.Controls.DataGridComboBoxColumn>())
        {
            column.ItemsSource = _viewModel.Countries;
        }

        // Force a binding refresh so visibility triggers pick up initial values.
        CustomersGrid.Items.Refresh();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

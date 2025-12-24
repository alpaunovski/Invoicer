using System.ComponentModel;
using System.Globalization;
using System.Windows;
using InvoiceDesk.Services;
using InvoiceDesk.ViewModels;

namespace InvoiceDesk.Views;

public partial class CustomerManagementWindow : Window
{
    private readonly CustomerManagementViewModel _viewModel;
    private readonly ILanguageService _languageService;

    public CustomerManagementWindow(CustomerManagementViewModel viewModel, ILanguageService languageService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _languageService = languageService;
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _languageService.CultureChanged += OnCultureChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

        UpdateEikVisibility();
        // Force a binding refresh so visibility triggers pick up initial values.
        CustomersGrid.Items.Refresh();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCultureChanged(object? sender, CultureInfo e)
    {
        UpdateEikVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomerManagementViewModel.IsBulgarianUi))
        {
            UpdateEikVisibility();
        }
    }

    private void UpdateEikVisibility()
    {
        CustomerEikColumn.Visibility = _viewModel.IsBulgarianUi ? Visibility.Visible : Visibility.Collapsed;
    }
}

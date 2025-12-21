using System.Windows;
using InvoiceDesk.Models;
using InvoiceDesk.ViewModels;
using Microsoft.Win32;

namespace InvoiceDesk.Views;

public partial class CompanyManagementWindow : Window
{
    private readonly CompanyManagementViewModel _viewModel;

    public CompanyManagementWindow(CompanyManagementViewModel viewModel)
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

    private void OnBrowseLogo(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not Company company)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Title = "Select logo image"
        };

        if (!string.IsNullOrWhiteSpace(company.LogoPath))
        {
            try
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(company.LogoPath);
                dialog.FileName = System.IO.Path.GetFileName(company.LogoPath);
            }
            catch
            {
                // ignore invalid initial path
            }
        }

        var result = dialog.ShowDialog(this);
        if (result == true)
        {
            company.LogoPath = dialog.FileName;
        }
    }
}

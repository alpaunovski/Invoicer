using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Models;
using InvoiceDesk.Services;

namespace InvoiceDesk.ViewModels;

public partial class CustomerManagementViewModel : ObservableObject
{
    private readonly CustomerService _customerService;
    private readonly ICompanyContext _companyContext;

    [ObservableProperty]
    private ObservableCollection<Customer> customers = new();

    public CustomerManagementViewModel(CustomerService customerService, ICompanyContext companyContext)
    {
        _customerService = customerService;
        _companyContext = companyContext;
    }

    public async Task LoadAsync()
    {
        var list = await _customerService.GetCustomersAsync();
        Customers = new ObservableCollection<Customer>(list);
    }

    [RelayCommand]
    private void AddCustomer()
    {
        Customers.Add(new Customer
        {
            CompanyId = _companyContext.CurrentCompanyId,
            Name = "",
            CountryCode = "",
            Address = "",
            Email = "",
            Phone = ""
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var customer in Customers)
        {
            await _customerService.SaveAsync(customer);
        }
    }
}

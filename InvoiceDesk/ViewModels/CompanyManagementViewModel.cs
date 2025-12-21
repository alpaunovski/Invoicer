using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Models;
using InvoiceDesk.Services;

namespace InvoiceDesk.ViewModels;

public partial class CompanyManagementViewModel : ObservableObject
{
    private readonly CompanyService _companyService;

    [ObservableProperty]
    private ObservableCollection<Company> companies = new();

    public CompanyManagementViewModel(CompanyService companyService)
    {
        _companyService = companyService;
    }

    public async Task LoadAsync()
    {
        var list = await _companyService.GetCompaniesAsync();
        Companies = new ObservableCollection<Company>(list);
    }

    [RelayCommand]
    private void AddCompany()
    {
        Companies.Add(new Company
        {
            Name = "",
            VatNumber = "",
            CountryCode = "",
            Address = "",
            BankIban = "",
            BankBic = ""
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var company in Companies)
        {
            await _companyService.SaveAsync(company);
        }
    }
}

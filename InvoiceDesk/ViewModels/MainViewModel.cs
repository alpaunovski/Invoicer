using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceDesk.Helpers;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;
using InvoiceDesk.Services;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CompanyService _companyService;
    private readonly CustomerService _customerService;
    private readonly InvoiceQueryService _invoiceQueryService;
    private readonly InvoiceService _invoiceService;
    private readonly PdfExportService _pdfExportService;
    private readonly ILanguageService _languageService;
    private readonly ICompanyContext _companyContext;
    private readonly UserSettingsService _settingsService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<Company> companies = new();

    [ObservableProperty]
    private Company? selectedCompany;

    [ObservableProperty]
    private ObservableCollection<Customer> customers = new();

    [ObservableProperty]
    private ObservableCollection<Invoice> invoices = new();

    [ObservableProperty]
    private Invoice? selectedInvoiceSummary;

    [ObservableProperty]
    private InvoiceViewModel? selectedInvoice;

    [ObservableProperty]
    private string? searchText;

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    [ObservableProperty]
    private Customer? selectedCustomerFilter;

    [ObservableProperty]
    private Customer? selectedCustomerForDraft;

    [ObservableProperty]
    private string selectedCulture = "en";

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<VatTypeOption> vatTypes = new();

    public XmlLanguage UiLanguage => XmlLanguage.GetLanguage(_languageService.CurrentCulture.IetfLanguageTag);

    public ObservableCollection<CultureOption> Cultures { get; } = new()
    {
        new CultureOption("en", "English"),
        new CultureOption("bg", "Български")
    };

    public MainViewModel(
        CompanyService companyService,
        CustomerService customerService,
        InvoiceQueryService invoiceQueryService,
        InvoiceService invoiceService,
        PdfExportService pdfExportService,
        ILanguageService languageService,
        ICompanyContext companyContext,
        UserSettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        _companyService = companyService;
        _customerService = customerService;
        _invoiceQueryService = invoiceQueryService;
        _invoiceService = invoiceService;
        _pdfExportService = pdfExportService;
        _languageService = languageService;
        _companyContext = companyContext;
        _settingsService = settingsService;
        _logger = logger;
        _companyContext.CompanyChanged += async (_, id) => await OnCompanyChangedAsync(id);
        _languageService.CultureChanged += (_, _) =>
        {
            RefreshVatTypes();
            OnPropertyChanged(nameof(UiLanguage));
        };

		// Ensure VAT options exist even before initialization completes.
		RefreshVatTypes();
    }

    partial void OnSelectedInvoiceSummaryChanged(Invoice? value)
    {
        if (value != null)
        {
            _ = SelectInvoiceAsync(value.Id);
        }
    }

    partial void OnSelectedCompanyChanged(Company? value)
    {
        if (value != null && ChangeCompanyCommand.CanExecute(null))
        {
            ChangeCompanyCommand.Execute(null);
        }
    }

    partial void OnSelectedCultureChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && ChangeCultureCommand.CanExecute(null))
        {
            ChangeCultureCommand.Execute(null);
        }
    }

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadAsync();
        SelectedCulture = settings.Culture;
        await _languageService.SetCultureAsync(SelectedCulture);
        RefreshVatTypes();

        var allCompanies = await _companyService.GetCompaniesAsync();
        Companies = new ObservableCollection<Company>(allCompanies);

        if (settings.CompanyId.HasValue)
        {
            SelectedCompany = Companies.FirstOrDefault(c => c.Id == settings.CompanyId.Value);
        }

        SelectedCompany ??= Companies.FirstOrDefault();
        if (SelectedCompany != null)
        {
            await _companyContext.SetCompanyAsync(SelectedCompany.Id);
            await LoadCustomersAsync();
            await LoadInvoicesAsync();
        }
    }

    private async Task OnCompanyChangedAsync(int companyId)
    {
        SelectedCompany = Companies.FirstOrDefault(c => c.Id == companyId);
        await LoadCustomersAsync();
        await LoadInvoicesAsync();
        var settings = await _settingsService.LoadAsync();
        settings.CompanyId = companyId;
        await _settingsService.SaveAsync(settings);
    }

    public async Task ReloadCompaniesAsync()
    {
        var currentId = SelectedCompany?.Id;
        var list = await _companyService.GetCompaniesAsync();
        Companies = new ObservableCollection<Company>(list);

        SelectedCompany = Companies.FirstOrDefault(c => c.Id == currentId) ?? Companies.FirstOrDefault();
        if (SelectedCompany != null)
        {
            await _companyContext.SetCompanyAsync(SelectedCompany.Id);
        }
    }

    public async Task ReloadCustomersAsync()
    {
        await LoadCustomersAsync();
        await LoadInvoicesAsync();
    }

    [RelayCommand]
    private async Task ChangeCompanyAsync()
    {
        if (SelectedCompany == null)
        {
            return;
        }

        await _companyContext.SetCompanyAsync(SelectedCompany.Id);
    }

    [RelayCommand]
    private async Task ChangeCultureAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedCulture))
        {
            return;
        }

        await _languageService.SetCultureAsync(SelectedCulture);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInvoicesAsync();
    }

    [RelayCommand]
    private async Task NewDraftAsync()
    {
        if (SelectedCompany == null || !Customers.Any())
        {
            StatusMessage = Strings.MessageSelectCompanyCustomers;
            return;
        }

        var customerId = SelectedCustomerForDraft?.Id;
        if (customerId == null)
        {
            StatusMessage = Strings.MessageSelectCustomer;
            return;
        }
        var draft = await _invoiceService.CreateDraftAsync(SelectedCompany.Id, customerId.Value);
        await LoadInvoicesAsync();
        await SelectInvoiceAsync(draft.Id);
        StatusMessage = Strings.MessageDraftCreated;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedInvoice == null)
        {
            return;
        }

        if (!SelectedInvoice.IsDraft)
        {
            StatusMessage = Strings.MessageSaveDraftOnly;
            return;
        }

        SelectedInvoice.RecalculateTotals();
        var entity = SelectedInvoice.ToEntity();
        await _invoiceService.SaveInvoiceAsync(entity, entity.Lines);
        await LoadInvoicesAsync();
        await SelectInvoiceAsync(entity.Id);
        StatusMessage = Strings.MessageInvoiceSaved;
    }

    [RelayCommand]
    private async Task IssueAsync()
    {
        if (SelectedInvoice == null)
        {
            return;
        }

        var invoice = await _invoiceService.IssueInvoiceAsync(SelectedInvoice.Id);
        await LoadInvoicesAsync();
        await SelectInvoiceAsync(invoice.Id);
        StatusMessage = string.Format(Strings.MessageInvoiceIssued, invoice.InvoiceNumber);
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (SelectedInvoice == null || !SelectedInvoice.IsIssued)
        {
            StatusMessage = Strings.MessagePdfIssuedOnly;
            return;
        }

        try
        {
            StatusMessage = Strings.MessageExportingPdf;
            var path = await _pdfExportService.ExportPdfAsync(SelectedInvoice.Id);
            StatusMessage = string.Format(Strings.MessagePdfExported, path);
            MessageBox.Show(StatusMessage, Strings.ExportPdf, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF export failed for invoice {InvoiceId}", SelectedInvoice.Id);
            StatusMessage = $"PDF export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddLine()
    {
        if (SelectedInvoice == null)
        {
            return;
        }

        SelectedInvoice.Lines.Add(new InvoiceLineViewModel
        {
            VatType = VatType.Domestic,
            Qty = 1,
            UnitPrice = 0,
            TaxRate = 0.2m
        });
        SelectedInvoice.RecalculateTotals();
    }

    [RelayCommand]
    private void RemoveLine(object? line)
    {
        if (SelectedInvoice == null)
        {
            return;
        }

        var vm = line as InvoiceLineViewModel;
        if (vm == null)
        {
            return;
        }

        SelectedInvoice.Lines.Remove(vm);
        SelectedInvoice.RecalculateTotals();
    }

    [RelayCommand]
    private async Task SelectInvoiceAsync(Invoice? invoice)
    {
        if (invoice == null)
        {
            SelectedInvoice = null;
            return;
        }

        await SelectInvoiceAsync(invoice.Id);
    }

    private async Task SelectInvoiceAsync(int invoiceId)
    {
        var detailed = await _invoiceQueryService.GetInvoiceWithLinesAsync(invoiceId);
        if (detailed == null)
        {
            return;
        }

        SelectedInvoice = InvoiceViewModel.FromEntity(detailed);
    }

    private async Task LoadInvoicesAsync()
    {
        var results = await _invoiceQueryService.SearchAsync(SearchText, FromDate, ToDate, SelectedCustomerFilter?.Id);
        Invoices = new ObservableCollection<Invoice>(results);
        if (SelectedInvoice != null)
        {
            SelectedInvoiceSummary = Invoices.FirstOrDefault(i => i.Id == SelectedInvoice.Id);
        }
        else
        {
            SelectedInvoiceSummary = Invoices.FirstOrDefault();
        }
    }

    private async Task LoadCustomersAsync()
    {
        var list = await _customerService.GetCustomersAsync();
        Customers = new ObservableCollection<Customer>(list);
        SelectedCustomerForDraft ??= Customers.FirstOrDefault();
    }

    private void RefreshVatTypes()
    {
        VatTypes = new ObservableCollection<VatTypeOption>(new[]
        {
            new VatTypeOption { Value = VatType.Domestic, Label = Strings.VatTypeDomestic },
            new VatTypeOption { Value = VatType.IntraEuReverseCharge, Label = Strings.VatTypeIntraEuReverseCharge },
            new VatTypeOption { Value = VatType.ExportOutsideEu, Label = Strings.VatTypeExportOutsideEu },
            new VatTypeOption { Value = VatType.VatExempt, Label = Strings.VatTypeExempt }
        });
    }
}

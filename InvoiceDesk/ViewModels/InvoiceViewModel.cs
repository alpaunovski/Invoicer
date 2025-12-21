using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InvoiceDesk.Models;

namespace InvoiceDesk.ViewModels;

public partial class InvoiceViewModel : ObservableObject
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private int companyId;

    [ObservableProperty]
    private int customerId;

    [ObservableProperty]
    private string invoiceNumber = string.Empty;

    [ObservableProperty]
    private DateTime issueDate = DateTime.Today;

    [ObservableProperty]
    private InvoiceStatus status = InvoiceStatus.Draft;

    [ObservableProperty]
    private string currency = "EUR";

    [ObservableProperty]
    private decimal subTotal;

    [ObservableProperty]
    private decimal taxTotal;

    [ObservableProperty]
    private decimal total;

    [ObservableProperty]
    private string customerNameSnapshot = string.Empty;

    [ObservableProperty]
    private string customerAddressSnapshot = string.Empty;

    [ObservableProperty]
    private string customerVatSnapshot = string.Empty;

    [ObservableProperty]
    private string? notes;

    [ObservableProperty]
    private ObservableCollection<InvoiceLineViewModel> lines = new();

    public bool IsDraft => Status == InvoiceStatus.Draft;
    public bool IsIssued => Status == InvoiceStatus.Issued;

    public void RecalculateTotals()
    {
        decimal subtotal = 0m;
        decimal tax = 0m;
        foreach (var line in Lines)
        {
            var baseAmount = Math.Round(line.Qty * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
            decimal vat = 0m;
            if (line.VatType == VatType.Domestic)
            {
                vat = Math.Round(baseAmount * line.TaxRate, 2, MidpointRounding.AwayFromZero);
            }

            line.LineTotal = baseAmount + vat;
            subtotal += baseAmount;
            tax += vat;
        }

        SubTotal = subtotal;
        TaxTotal = tax;
        Total = subtotal + tax;
    }

    public static InvoiceViewModel FromEntity(Invoice invoice)
    {
        return new InvoiceViewModel
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            CustomerId = invoice.CustomerId,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            Status = invoice.Status,
            Currency = invoice.Currency,
            SubTotal = invoice.SubTotal,
            TaxTotal = invoice.TaxTotal,
            Total = invoice.Total,
            CustomerNameSnapshot = invoice.CustomerNameSnapshot,
            CustomerAddressSnapshot = invoice.CustomerAddressSnapshot,
            CustomerVatSnapshot = invoice.CustomerVatSnapshot,
            Notes = invoice.Notes,
            Lines = new ObservableCollection<InvoiceLineViewModel>(invoice.Lines.Select(InvoiceLineViewModel.FromEntity))
        };
    }

    public Invoice ToEntity()
    {
        return new Invoice
        {
            Id = Id,
            CompanyId = CompanyId,
            CustomerId = CustomerId,
            InvoiceNumber = InvoiceNumber,
            IssueDate = IssueDate,
            Status = Status,
            Currency = Currency,
            SubTotal = SubTotal,
            TaxTotal = TaxTotal,
            Total = Total,
            CustomerNameSnapshot = CustomerNameSnapshot,
            CustomerAddressSnapshot = CustomerAddressSnapshot,
            CustomerVatSnapshot = CustomerVatSnapshot,
            Notes = Notes,
            Lines = Lines.Select(l => l.ToEntity(CompanyId)).ToList()
        };
    }
}

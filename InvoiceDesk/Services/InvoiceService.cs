using InvoiceDesk.Data;
using InvoiceDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Services;

public class InvoiceService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICompanyContext _companyContext;
    private readonly PdfExportService _pdfExportService;
    private readonly ILogger<InvoiceService> _logger;
    private const string DraftPrefix = "DRAFT";

    public InvoiceService(IDbContextFactory<AppDbContext> dbFactory, ICompanyContext companyContext, PdfExportService pdfExportService, ILogger<InvoiceService> logger)
    {
        _dbFactory = dbFactory;
        _companyContext = companyContext;
        _pdfExportService = pdfExportService;
        _logger = logger;
    }

    public async Task<Invoice> CreateDraftAsync(int companyId, int customerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken);
        if (customer == null)
        {
            throw new InvalidOperationException("Customer not found for company");
        }

        var invoice = new Invoice
        {
            CompanyId = companyId,
            CustomerId = customerId,
            InvoiceNumber = GenerateDraftNumber(companyId),
            Status = InvoiceStatus.Draft,
            IssueDate = DateTime.Today,
            Currency = "EUR",
            CustomerNameSnapshot = customer.Name,
            CustomerAddressSnapshot = customer.Address,
            CustomerVatSnapshot = customer.VatNumber
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<Invoice> SaveInvoiceAsync(Invoice invoice, IEnumerable<InvoiceLine> lines, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Invoices.Include(i => i.Lines).Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoice.Id && i.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);

        if (existing == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (existing.Status != InvoiceStatus.Draft || existing.IssuedAtUtc != null)
        {
            throw new InvalidOperationException("Issued invoices cannot be edited");
        }

        existing.CustomerId = invoice.CustomerId;
        existing.IssueDate = invoice.IssueDate;
        existing.Currency = invoice.Currency;
        existing.Notes = invoice.Notes;

        if (string.IsNullOrWhiteSpace(existing.InvoiceNumber))
        {
            existing.InvoiceNumber = GenerateDraftNumber(existing.CompanyId);
        }

        var customerValid = await db.Customers.AnyAsync(c => c.Id == existing.CustomerId && c.CompanyId == existing.CompanyId, cancellationToken);
        if (!customerValid)
        {
            throw new InvalidOperationException("Customer not found for this company");
        }

        await db.Entry(existing).Reference(i => i.Customer).LoadAsync(cancellationToken);
        if (existing.Customer != null)
        {
            existing.CustomerNameSnapshot = existing.Customer.Name;
            existing.CustomerAddressSnapshot = existing.Customer.Address;
            existing.CustomerVatSnapshot = existing.Customer.VatNumber;
        }

        UpdateLines(existing, lines);
        ApplyTotals(existing);

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<Invoice> IssueInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Customer)
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == _companyContext.CurrentCompanyId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.Status != InvoiceStatus.Draft || invoice.IssuedAtUtc != null)
        {
            throw new InvalidOperationException("Only draft invoices can be issued");
        }

        var company = invoice.Company ?? throw new InvalidOperationException("Invoice company missing");
        var customer = invoice.Customer ?? throw new InvalidOperationException("Invoice customer missing");

        var invoiceNumber = string.IsNullOrWhiteSpace(company.InvoiceNumberPrefix)
            ? company.NextInvoiceNumber.ToString()
            : $"{company.InvoiceNumberPrefix}{company.NextInvoiceNumber}";
        company.NextInvoiceNumber += 1;

        invoice.InvoiceNumber = invoiceNumber;
        invoice.CustomerNameSnapshot = customer.Name;
        invoice.CustomerAddressSnapshot = customer.Address;
        invoice.CustomerVatSnapshot = customer.VatNumber;
        invoice.IssuedAtUtc = DateTime.UtcNow;
        invoice.Status = InvoiceStatus.Issued;
        ApplyTotals(invoice);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _pdfExportService.GenerateAndStoreIssuedPdfAsync(invoice.Id, cancellationToken);
        _logger.LogInformation("Invoice {InvoiceId} issued with number {InvoiceNumber}", invoice.Id, invoice.InvoiceNumber);
        return invoice;
    }

    private static string GenerateDraftNumber(int companyId)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var rand = Random.Shared.Next(1000, 9999);
        return $"{DraftPrefix}-{companyId}-{stamp}-{rand}";
    }

    private static void UpdateLines(Invoice invoice, IEnumerable<InvoiceLine> incoming)
    {
        var incomingList = incoming.ToList();
        var toRemove = invoice.Lines.Where(l => incomingList.All(i => i.Id != l.Id)).ToList();
        foreach (var remove in toRemove)
        {
            invoice.Lines.Remove(remove);
        }

        foreach (var incomingLine in incomingList)
        {
            var target = invoice.Lines.FirstOrDefault(l => l.Id == incomingLine.Id);
            if (target == null)
            {
                incomingLine.CompanyId = invoice.CompanyId;
                incomingLine.InvoiceId = invoice.Id;
                invoice.Lines.Add(incomingLine);
            }
            else
            {
                target.CompanyId = invoice.CompanyId;
                target.InvoiceId = invoice.Id;
                target.Description = incomingLine.Description;
                target.Qty = incomingLine.Qty;
                target.UnitPrice = incomingLine.UnitPrice;
                target.TaxRate = incomingLine.TaxRate;
                target.VatType = incomingLine.VatType;
            }
        }
    }

    private static void ApplyTotals(Invoice invoice)
    {
        decimal subtotal = 0m;
        decimal taxTotal = 0m;

        foreach (var line in invoice.Lines)
        {
            var lineBase = Math.Round(line.Qty * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
            decimal vat = 0m;
            switch (line.VatType)
            {
                case VatType.Domestic:
                    vat = Math.Round(lineBase * line.TaxRate, 2, MidpointRounding.AwayFromZero);
                    break;
                case VatType.IntraEuReverseCharge:
                case VatType.ExportOutsideEu:
                case VatType.VatExempt:
                    vat = 0m;
                    break;
            }

            line.LineTotal = lineBase + vat;
            subtotal += lineBase;
            taxTotal += vat;
        }

        invoice.SubTotal = subtotal;
        invoice.TaxTotal = taxTotal;
        invoice.Total = subtotal + taxTotal;
    }
}

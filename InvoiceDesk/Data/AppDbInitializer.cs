using InvoiceDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Data;

public class AppDbInitializer
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<AppDbInitializer> _logger;

    public AppDbInitializer(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<AppDbInitializer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        await FixMissingInvoiceNumbersAsync(db, cancellationToken);

        if (!await db.Companies.AnyAsync(cancellationToken))
        {
            var company = new Company
            {
                Name = "Default Company",
                VatNumber = "BG000000000",
                CountryCode = "BG",
                Address = "",
                BankIban = "",
                BankBic = "",
                InvoiceNumberPrefix = "INV",
                NextInvoiceNumber = 1
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded initial company");
        }
    }

    private async Task FixMissingInvoiceNumbersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var missing = await db.Invoices
            .Where(i => string.IsNullOrWhiteSpace(i.InvoiceNumber))
            .ToListAsync(cancellationToken);

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var invoice in missing)
        {
            invoice.InvoiceNumber = GenerateRepairNumber(invoice.CompanyId, invoice.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Backfilled {Count} invoices missing numbers", missing.Count);
    }

    private static string GenerateRepairNumber(int companyId, int invoiceId)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        return $"DRAFTFIX-{companyId}-{invoiceId}-{stamp}";
    }
}

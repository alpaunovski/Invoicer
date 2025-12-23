using InvoiceDesk.Data;
using InvoiceDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceDesk.Services;

public class CompanyService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public CompanyService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<Company?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Company> SaveAsync(Company company, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (company.Id == 0)
        {
            db.Companies.Add(company);
        }
        else
        {
            db.Companies.Update(company);
        }

        await db.SaveChangesAsync(cancellationToken);
        return company;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        db.Companies.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}

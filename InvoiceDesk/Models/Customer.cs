using System.Collections.Generic;

namespace InvoiceDesk.Models;

public class Customer
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsVatRegistered { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public Company? Company { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

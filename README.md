# InvoiceDesk (WPF, .NET 8)

Multi-company invoice manager with MySQL + EF Core, WebView2 PDF export, MVVM, and RESX localization (English/Bulgarian).

# Disclaimer

I built this program as a course paper for University. It's not good for production. 
Do not use it in a real business setting.
This program comes with absolutely no warranty, given or implied.
It's for educational purposes only.

## Prereqs
- .NET 8 SDK
- MySQL 8 server running locally
- WebView2 Runtime (for PDF export)

## Configure
1) Edit `InvoiceDesk/appsettings.json` connection string (`ConnectionStrings:Default`).
2) Ensure the MySQL user has create/alter permissions for the database.

## Database
```
cd InvoiceDesk
dotnet ef migrations add InitialCreate
dotnet ef database update
```
Uses Pomelo.EntityFrameworkCore.MySql with IDbContextFactory and migrations (no EnsureCreated in production). The app seeds one default Company on first run if DB is empty.

## Run
```
dotnet run --project InvoiceDesk
```

## Features
- Strict multi-company isolation; all queries filter by current company
- Transactional invoice issuing with per-company numbering and immutable issued invoices/PDFs
- EF Core precision and indexes per requirements
- PDF via WebView2 PrintToPdfAsync with pagination-safe HTML/CSS
- Localization: English/Bulgarian with runtime culture switching and persisted user preference
- UI: company + language selectors, invoice list/search, draft editor, issue/export actions, company/customer management windows

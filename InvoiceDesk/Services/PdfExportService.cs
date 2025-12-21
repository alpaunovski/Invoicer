using System.IO;
using System.Windows;
using InvoiceDesk.Data;
using InvoiceDesk.Helpers;
using InvoiceDesk.Models;
using InvoiceDesk.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace InvoiceDesk.Services;

public class PdfExportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly InvoiceHtmlRenderer _renderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(IDbContextFactory<AppDbContext> dbFactory, InvoiceHtmlRenderer renderer, IConfiguration configuration, ILogger<PdfExportService> logger)
    {
        _dbFactory = dbFactory;
        _renderer = renderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> ExportPdfAsync(int invoiceId, string? targetPath = null, bool regenerate = false, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Company)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.IssuedAtUtc == null)
        {
            throw new InvalidOperationException("Only issued invoices can be exported");
        }

        var outputDir = targetPath != null ? Path.GetDirectoryName(targetPath)! : GetOutputDirectory();
        Directory.CreateDirectory(outputDir);

        var fileName = invoice.IssuedPdfFileName ?? $"Invoice-{invoice.InvoiceNumber}.pdf";
        var outputPath = targetPath ?? Path.Combine(outputDir, fileName);

        if (invoice.IssuedPdf != null && !regenerate)
        {
            await File.WriteAllBytesAsync(outputPath, invoice.IssuedPdf, cancellationToken);
            return outputPath;
        }

        var html = _renderer.RenderHtml(invoice.Company!, invoice, invoice.Lines.ToList());
        var bytes = await GeneratePdfBytesAsync(html, outputPath, cancellationToken);

        invoice.IssuedPdf = bytes;
        invoice.IssuedPdfFileName = fileName;
        invoice.IssuedPdfSha256 = HashHelper.ComputeSha256(bytes);
        invoice.IssuedPdfCreatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Exported PDF for invoice {InvoiceId} to {Path}", invoiceId, outputPath);
        return outputPath;
    }

    public Task<string> GenerateAndStoreIssuedPdfAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        return ExportPdfAsync(invoiceId, null, true, cancellationToken);
    }

    private string GetOutputDirectory()
    {
        var configured = _configuration.GetSection("Pdf")?["OutputDirectory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        var defaultDir = Path.Combine(AppContext.BaseDirectory, "Exports");
        return defaultDir;
    }

    private async Task<byte[]> GeneratePdfBytesAsync(string html, string filePath, CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher == null)
        {
            throw new InvalidOperationException("No WPF dispatcher available");
        }

        return await dispatcher.InvokeAsync(async () =>
        {
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InvoiceDesk", "WebView2");
            Directory.CreateDirectory(userData);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);

            var webView = new WebView2
            {
                Visibility = Visibility.Collapsed,
                Width = 1240,
                Height = 1754
            };

            await webView.EnsureCoreWebView2Async(environment);
            var navigationCompleted = new TaskCompletionSource<bool>();
            webView.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    navigationCompleted.TrySetResult(true);
                }
                else
                {
                    navigationCompleted.TrySetException(new InvalidOperationException($"Navigation failed: {args.WebErrorStatus}"));
                }
            };

            webView.NavigateToString(html);
            await navigationCompleted.Task.WaitAsync(cancellationToken);

            var settings = webView.CoreWebView2.Environment.CreatePrintSettings();
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintSelectionOnly = false;
            settings.ShouldPrintHeaderAndFooter = false;
            settings.MarginBottom = 0.5;
            settings.MarginTop = 0.5;
            settings.MarginLeft = 0.5;
            settings.MarginRight = 0.5;

            await webView.CoreWebView2.PrintToPdfAsync(filePath, settings).WaitAsync(cancellationToken);
            return await File.ReadAllBytesAsync(filePath, cancellationToken);
        }).Task.Unwrap();
    }
}

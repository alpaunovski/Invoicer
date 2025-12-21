using System.Globalization;
using System.Text;
using System.IO;
using InvoiceDesk.Models;
using InvoiceDesk.Resources;

namespace InvoiceDesk.Rendering;

public class InvoiceHtmlRenderer
{
    public string RenderHtml(Company company, Invoice invoice, IList<InvoiceLine> lines)
    {
        // Use invariant culture so numeric formatting stays stable regardless of UI locale.
        var culture = CultureInfo.InvariantCulture;
        var vatSummary = BuildVatSummary(lines, culture);
        var legalTexts = BuildLegalTexts(lines);

        // Embed company logo as data URL if available to avoid external file dependencies.
        var logoDataUrl = BuildLogoDataUrl(company);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
        sb.Append("<style>");
        sb.Append("@page { size: A4; margin: 20mm; } body { font-family: 'Segoe UI', sans-serif; -webkit-print-color-adjust: exact; } ");
        sb.Append("h1,h2,h3,h4 { margin: 0; } .header, .footer { position: fixed; left: 0; right: 0; } .header { top: 0; } .footer { bottom: 0; font-size: 12px; } ");
        sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 12px; } th, td { border: 1px solid #444; padding: 6px; font-size: 12px; } ");
        sb.Append("thead { display: table-header-group; } tfoot { display: table-footer-group; } tr { page-break-inside: avoid; } ");
        sb.Append(".totals { margin-top: 16px; width: 40%; float: right; page-break-inside: avoid; } .totals td { border: none; } .totals tr td.label { text-align: right; font-weight: 600; } ");
        sb.Append(".address-block { width: 48%; display: inline-block; vertical-align: top; } .meta { margin-top: 10px; } .badge { padding: 4px 8px; border-radius: 4px; background: #223a5e; color: white; font-size: 12px; display: inline-block; } ");
        sb.Append(".notes { margin-top: 20px; } .legal { font-size: 11px; color: #333; margin-top: 12px; } ");
        sb.Append(".logo-wrap { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; } .logo { max-height: 80px; display: block; } .company-title { margin: 0; }");
        sb.Append("</style></head><body>");

        sb.Append("<div class='header'>");
        if (!string.IsNullOrEmpty(logoDataUrl))
        {
            sb.Append("<div class='logo-wrap'>");
            sb.Append($"<img class='logo' src='{logoDataUrl}' alt='{Html(company.Name)} logo' />");
            sb.Append($"<h2 class='company-title'>{Html(company.Name)}</h2>");
            sb.Append("</div>");
        }
        else
        {
            sb.Append($"<h2 class='company-title'>{Html(company.Name)}</h2>");
        }
        sb.Append("</div>");

        sb.Append("<div class='meta' style='margin-top:40px;'>");
        sb.Append($"<span class='badge'>{Html(GetStatusLabel(invoice.Status))}</span>");
        sb.Append("</div>");

        sb.Append("<div style='margin-top:20px;'>");
        sb.Append("<div class='address-block'>");
        sb.Append($"<h3>{Html(Strings.PdfCompany)}</h3>");
        sb.Append($"<div>{Html(company.Name)}</div>");
        sb.Append($"<div>{Html(company.Address)}</div>");
        sb.Append($"<div>{Html(company.CountryCode)} | {Html(company.VatNumber)}</div>");
        sb.Append($"<div>{Html(company.BankIban)} / {Html(company.BankBic)}</div>");
        sb.Append("</div>");

        sb.Append("<div class='address-block'>");
        sb.Append($"<h3>{Html(Strings.PdfCustomer)}</h3>");
        sb.Append($"<div>{Html(invoice.CustomerNameSnapshot)}</div>");
        sb.Append($"<div>{Html(invoice.CustomerAddressSnapshot)}</div>");
        sb.Append($"<div>{Html(invoice.CustomerVatSnapshot)}</div>");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("<div class='meta'>");
        sb.Append($"<div>{Html(Strings.PdfInvoiceNumber)}: {Html(invoice.InvoiceNumber)}</div>");
        sb.Append($"<div>{Html(Strings.PdfIssueDate)}: {invoice.IssueDate.ToString("yyyy-MM-dd", culture)}</div>");
        sb.Append($"<div>{Html(Strings.StatusLabel)}: {Html(GetStatusLabel(invoice.Status))}</div>");
        sb.Append("</div>");

        sb.Append("<table><thead><tr>");
        sb.Append($"<th>{Html(Strings.DescriptionLabel)}</th>");
        sb.Append($"<th>{Html(Strings.QuantityLabel)}</th>");
        sb.Append($"<th>{Html(Strings.UnitPriceLabel)}</th>");
        sb.Append($"<th>{Html(Strings.TaxRateLabel)}</th>");
        sb.Append($"<th>{Html(Strings.VatTypeLabel)}</th>");
        sb.Append($"<th>{Html(Strings.Total)}</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var line in lines)
        {
            // Render each line with fixed decimal formats to keep totals deterministic.
            sb.Append("<tr>");
            sb.Append($"<td>{Html(line.Description)}</td>");
            sb.Append($"<td style='text-align:right'>{line.Qty.ToString("0.###", culture)}</td>");
            sb.Append($"<td style='text-align:right'>{line.UnitPrice.ToString("0.00", culture)}</td>");
            sb.Append($"<td style='text-align:right'>{line.TaxRate.ToString("0.####", culture)}</td>");
            sb.Append($"<td>{Html(GetVatLabel(line.VatType))}</td>");
            sb.Append($"<td style='text-align:right'>{line.LineTotal.ToString("0.00", culture)}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");

        sb.Append("<table class='totals'>");
        sb.Append($"<tr><td class='label'>{Html(Strings.PdfSubTotalLabel)}</td><td style='text-align:right'>{invoice.SubTotal.ToString("0.00", culture)}</td></tr>");
        sb.Append($"<tr><td class='label'>{Html(Strings.PdfTaxTotalLabel)}</td><td style='text-align:right'>{invoice.TaxTotal.ToString("0.00", culture)}</td></tr>");
        sb.Append($"<tr><td class='label'>{Html(Strings.PdfGrandTotalLabel)}</td><td style='text-align:right'>{invoice.Total.ToString("0.00", culture)}</td></tr>");
        sb.Append("</table>");

        sb.Append("<div style='clear:both;'></div>");

        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            sb.Append($"<div class='notes'><strong>{Html(Strings.PdfNotes)}</strong><div>{Html(invoice.Notes!)}</div></div>");
        }

        if (vatSummary.Any())
        {
            // Show VAT aggregation per type so reverse charge/export context is visible.
            sb.Append($"<div class='legal'><strong>{Html(Strings.PdfVatSummary)}</strong><ul>");
            foreach (var item in vatSummary)
            {
                sb.Append($"<li>{Html(item)}</li>");
            }
            sb.Append("</ul></div>");
        }

        if (legalTexts.Any())
        {
            // Include legal text blocks when specific VAT regimes require wording.
            sb.Append("<div class='legal'>");
            foreach (var text in legalTexts)
            {
                sb.Append($"<div>{Html(text)}</div>");
            }
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string? BuildLogoDataUrl(Company company)
    {
        if (string.IsNullOrWhiteSpace(company.LogoPath))
        {
            return null;
        }

        try
        {
            var path = company.LogoPath;
            if (!Path.IsPathRooted(path))
            {
                var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                path = Path.GetFullPath(Path.Combine(baseDir, path));
            }

            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "image/png"
            };
            var base64 = Convert.ToBase64String(bytes);
            return $"data:{mime};base64,{base64}";
        }
        catch
        {
            return null;
        }
    }

    private static string Html(string? value)
    {
        return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string GetVatLabel(VatType vatType)
    {
        return vatType switch
        {
            VatType.Domestic => Strings.VatTypeDomestic,
            VatType.IntraEuReverseCharge => Strings.VatTypeIntraEuReverseCharge,
            VatType.ExportOutsideEu => Strings.VatTypeExportOutsideEu,
            VatType.VatExempt => Strings.VatTypeExempt,
            _ => vatType.ToString()
        };
    }

    private static string GetStatusLabel(InvoiceStatus status)
    {
        return status switch
        {
            InvoiceStatus.Draft => Strings.StatusDraft,
            InvoiceStatus.Issued => Strings.StatusIssued,
            InvoiceStatus.Paid => Strings.StatusPaid,
            InvoiceStatus.Cancelled => Strings.StatusCancelled,
            _ => status.ToString()
        };
    }

    private static List<string> BuildVatSummary(IEnumerable<InvoiceLine> lines, CultureInfo culture)
    {
        var grouped = lines.GroupBy(l => l.VatType);
        var items = new List<string>();
        foreach (var group in grouped)
        {
            var amount = group.Sum(l => l.LineTotal);
            var label = GetVatLabel(group.Key);
            items.Add($"{label}: {amount.ToString("0.00", culture)}");
        }

        return items;
    }

    private static List<string> BuildLegalTexts(IEnumerable<InvoiceLine> lines)
    {
        var texts = new List<string>();
        if (lines.Any(l => l.VatType == VatType.IntraEuReverseCharge))
        {
            texts.Add(Strings.PdfLegalReverseCharge);
        }
        if (lines.Any(l => l.VatType == VatType.ExportOutsideEu))
        {
            texts.Add(Strings.PdfLegalExport);
        }
        if (lines.Any(l => l.VatType == VatType.VatExempt))
        {
            texts.Add(Strings.PdfLegalVatExempt);
        }
        return texts;
    }
}

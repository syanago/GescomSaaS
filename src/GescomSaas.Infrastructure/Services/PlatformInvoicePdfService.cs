using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GescomSaas.Infrastructure.Services;

public class PlatformInvoicePdfService(ApplicationDbContext dbContext) : IPlatformInvoicePdfService
{
    public async Task<CommercialDocumentPdfResult> GeneratePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var invoice = await dbContext.PlatformInvoices
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null || invoice.Tenant is null)
        {
            throw new NotFoundException(nameof(PlatformInvoice), invoiceId);
        }

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(header => ComposeHeader(header, invoice));
                page.Content().Element(content => ComposeContent(content, invoice));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Genere par GescomSaas");
                    text.Span(" - ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return new CommercialDocumentPdfResult(
            $"Facture-plateforme-{invoice.InvoiceNumber}.pdf",
            "application/pdf",
            pdfBytes);
    }

    private static void ComposeHeader(IContainer container, PlatformInvoice invoice)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("GescomSaas Platform").FontSize(18).Bold();
                    left.Item().Text(invoice.Tenant?.CompanyName ?? "-").FontColor(Colors.Grey.Darken2);
                    left.Item().Text(invoice.Tenant?.PrimaryContactEmail ?? "-").FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(220).Column(right =>
                {
                    right.Item().AlignRight().Text("Facture plateforme").FontSize(20).Bold();
                    right.Item().AlignRight().Text(invoice.InvoiceNumber).FontSize(12);
                    right.Item().AlignRight().Text($"Statut: {invoice.Status}");
                });
            });

            column.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text($"Emission: {invoice.IssueDate:dd/MM/yyyy}");
                    info.Item().Text($"Echeance: {invoice.DueDate:dd/MM/yyyy}");
                    info.Item().Text($"Devise: {invoice.CurrencyCode}");
                });

                row.RelativeItem().Column(info =>
                {
                    info.Item().Text($"Periode: {invoice.PeriodStart:dd/MM/yyyy} - {invoice.PeriodEnd:dd/MM/yyyy}");
                    info.Item().Text($"Reglee le: {(invoice.PaidOn.HasValue ? invoice.PaidOn.Value.ToString("dd/MM/yyyy") : "-")}");
                    info.Item().Text($"Tenant: {invoice.Tenant?.CompanyName ?? "-"}");
                });
            });

            column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, PlatformInvoice invoice)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.8f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(1.1f);
                });

                table.Header(header =>
                {
                    static IContainer HeaderCell(IContainer cell) =>
                        cell.Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(x => x.SemiBold());

                    header.Cell().Element(HeaderCell).Text("Description");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Qte");
                    header.Cell().Element(HeaderCell).AlignRight().Text("PU HT");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Taxe");
                    header.Cell().Element(HeaderCell).AlignRight().Text("TTC");
                });

                foreach (var line in invoice.Lines.OrderBy(x => x.CreatedOnUtc))
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(line.Description);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.Quantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.UnitPriceExcludingTax.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{line.TaxRate:N2}%");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.LineTotalIncludingTax.ToString("N2"));
                }
            });

            column.Item().PaddingTop(16).AlignRight().Width(220).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                void TotalRow(string label, decimal amount)
                {
                    table.Cell().Padding(4).Text(label).SemiBold();
                    table.Cell().Padding(4).AlignRight().Text(amount.ToString("N2"));
                }

                TotalRow("Total HT", invoice.TotalExcludingTax);
                TotalRow("Taxes", invoice.TotalTax);
                TotalRow("Total TTC", invoice.TotalIncludingTax);
            });

            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                column.Item().PaddingTop(18).Text("Notes").Bold();
                column.Item().PaddingTop(4).Text(invoice.Notes);
            }
        });
    }
}

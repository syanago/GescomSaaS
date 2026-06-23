using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GescomSaas.Infrastructure.Services;

public class CommercialDocumentPdfService(ApplicationDbContext dbContext) : ICommercialDocumentPdfService
{
    public async Task<CommercialDocumentPdfResult> GeneratePdfAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        var document = await dbContext.CommercialDocuments
            .AsNoTracking()
            .Include(x => x.Partner)
            .Include(x => x.Warehouse)
            .Include(x => x.SourceDocument)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

        if (tenant is null)
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        if (document is null)
        {
            throw new NotFoundException(nameof(CommercialDocument), documentId);
        }

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(header => ComposeHeader(header, tenant.CompanyName, document));
                page.Content().Element(content => ComposeContent(content, document));
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

        var fileName = $"{GetDocumentLabel(document.DocumentType).Replace(' ', '-')}-{document.Number}.pdf";
        return new CommercialDocumentPdfResult(fileName, "application/pdf", pdfBytes);
    }

    private static void ComposeHeader(IContainer container, string companyName, Domain.Entities.Commercial.CommercialDocument document)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(companyName).FontSize(18).Bold();
                    left.Item().Text("Gestion commerciale SaaS").FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(210).Column(right =>
                {
                    right.Item().AlignRight().Text(GetDocumentLabel(document.DocumentType)).FontSize(20).Bold();
                    right.Item().AlignRight().Text(document.Number).FontSize(12);
                    right.Item().AlignRight().Text($"Statut: {document.Status}");
                });
            });

            column.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Item().Text($"Date: {document.DocumentDate:dd/MM/yyyy}");
                    info.Item().Text($"Echeance: {(document.DueDate.HasValue ? document.DueDate.Value.ToString("dd/MM/yyyy") : "-")}");
                    info.Item().Text($"Devise: {document.CurrencyCode}");
                });

                row.RelativeItem().Column(info =>
                {
                    info.Item().Text($"Tiers: {document.Partner?.Name ?? "-"}");
                    info.Item().Text($"Depot: {document.Warehouse?.Label ?? "-"}");
                    info.Item().Text($"Source: {document.SourceDocument?.Number ?? "-"}");
                });
            });

            column.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, Domain.Entities.Commercial.CommercialDocument document)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(1.1f);
                });

                table.Header(header =>
                {
                    static IContainer HeaderCell(IContainer cell) =>
                        cell.Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(x => x.SemiBold());

                    header.Cell().Element(HeaderCell).Text("Article");
                    header.Cell().Element(HeaderCell).Text("Description");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Qte");
                    header.Cell().Element(HeaderCell).AlignRight().Text("PU HT");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Rem.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Taxe");
                    header.Cell().Element(HeaderCell).AlignRight().Text("TTC");
                });

                foreach (var line in document.Lines)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(line.Product?.Sku ?? "-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(line.Description);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.Quantity.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(line.UnitPriceExcludingTax.ToString("N2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{line.DiscountRate:N2}%");
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

                TotalRow("Total HT", document.TotalExcludingTax);
                TotalRow("Taxes", document.TotalTax);
                TotalRow("Total TTC", document.TotalIncludingTax);
            });

            if (!string.IsNullOrWhiteSpace(document.Notes))
            {
                column.Item().PaddingTop(18).Text("Notes").Bold();
                column.Item().PaddingTop(4).Text(document.Notes);
            }
        });
    }

    private static string GetDocumentLabel(CommercialDocumentType type) => type switch
    {
        CommercialDocumentType.SalesQuote => "Devis",
        CommercialDocumentType.SalesOrder => "Commande client",
        CommercialDocumentType.DeliveryNote => "Bon de livraison",
        CommercialDocumentType.SalesInvoice => "Facture",
        CommercialDocumentType.SalesCreditNote => "Avoir client",
        CommercialDocumentType.PurchaseRequest => "Demande d'achat",
        CommercialDocumentType.PurchaseOrder => "Commande fournisseur",
        CommercialDocumentType.GoodsReceipt => "Reception",
        CommercialDocumentType.PurchaseInvoice => "Facture fournisseur",
        CommercialDocumentType.SupplierCreditNote => "Avoir fournisseur",
        _ => type.ToString()
    };
}

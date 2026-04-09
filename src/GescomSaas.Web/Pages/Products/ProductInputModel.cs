using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Products;

public class ProductInputModel
{
    [Required]
    [Display(Name = "Reference")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Type")]
    public ProductType ProductType { get; set; } = ProductType.StockItem;

    [Display(Name = "Unite")]
    public string UnitOfMeasure { get; set; } = "UN";

    [Display(Name = "Suivi de stock")]
    public bool TrackStock { get; set; } = true;

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Famille")]
    public Guid? ProductCategoryId { get; set; }

    [Display(Name = "Taxe")]
    public Guid? TaxCodeId { get; set; }

    [Display(Name = "Prix d'achat")]
    public decimal PurchasePrice { get; set; }

    [Display(Name = "Prix de vente")]
    public decimal SalesPrice { get; set; }

    public static ProductInputModel FromEntity(Product entity) =>
        new()
        {
            Sku = entity.Sku,
            Label = entity.Label,
            Description = entity.Description,
            ProductType = entity.ProductType,
            UnitOfMeasure = entity.UnitOfMeasure,
            TrackStock = entity.TrackStock,
            IsActive = entity.IsActive,
            ProductCategoryId = entity.ProductCategoryId,
            TaxCodeId = entity.TaxCodeId,
            PurchasePrice = entity.PurchasePrice,
            SalesPrice = entity.SalesPrice
        };

    public void ApplyTo(Product entity)
    {
        entity.Sku = Sku.Trim();
        entity.Label = Label.Trim();
        entity.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        entity.ProductType = ProductType;
        entity.UnitOfMeasure = string.IsNullOrWhiteSpace(UnitOfMeasure) ? "UN" : UnitOfMeasure.Trim().ToUpperInvariant();
        entity.TrackStock = TrackStock;
        entity.IsActive = IsActive;
        entity.ProductCategoryId = ProductCategoryId;
        entity.TaxCodeId = TaxCodeId;
        entity.PurchasePrice = PurchasePrice;
        entity.SalesPrice = SalesPrice;
    }
}

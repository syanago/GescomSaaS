using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.ProductCategories;

public class ProductCategoryInputModel
{
    [Required]
    [Display(Name = "Code famille")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Suivi de stock")]
    public StockValuationMethod StockValuationMethod { get; set; } = StockValuationMethod.Cmup;

    [Display(Name = "Gestion lot / serie")]
    public StockIdentityTrackingMode StockIdentityTrackingMode { get; set; } = StockIdentityTrackingMode.None;

    public static ProductCategoryInputModel FromEntity(ProductCategory entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            StockValuationMethod = entity.StockValuationMethod,
            StockIdentityTrackingMode = entity.StockIdentityTrackingMode
        };

    public void ApplyTo(ProductCategory entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.StockValuationMethod = StockValuationMethod;
        entity.StockIdentityTrackingMode = StockIdentityTrackingMode;
    }
}

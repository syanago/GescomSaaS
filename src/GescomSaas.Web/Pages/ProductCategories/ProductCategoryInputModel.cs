using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.ProductCategories;

public class ProductCategoryInputModel
{
    [Required]
    [Display(Name = "Code famille")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    public static ProductCategoryInputModel FromEntity(ProductCategory entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label
        };

    public void ApplyTo(ProductCategory entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
    }
}

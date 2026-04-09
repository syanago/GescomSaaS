using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;

namespace GescomSaas.Web.Pages.Warehouses;

public class WarehouseInputModel
{
    [Required]
    [Display(Name = "Code depot")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Libelle")]
    public string Label { get; set; } = string.Empty;

    [Display(Name = "Depot par defaut")]
    public bool IsDefault { get; set; }

    public static WarehouseInputModel FromEntity(Warehouse entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            IsDefault = entity.IsDefault
        };

    public void ApplyTo(Warehouse entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.IsDefault = IsDefault;
    }
}

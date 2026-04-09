using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Inventory;

public class StockAdjustmentInputModel
{
    [Required]
    [Display(Name = "Article")]
    public Guid? ProductId { get; set; }

    [Required]
    [Display(Name = "Depot")]
    public Guid? WarehouseId { get; set; }

    [Display(Name = "Date mouvement")]
    [DataType(DataType.Date)]
    public DateOnly MovementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Type d'ajustement")]
    public StockMovementType MovementType { get; set; } = StockMovementType.AdjustmentIn;

    [Display(Name = "Quantite")]
    public decimal Quantity { get; set; } = 1m;

    [Display(Name = "Cout unitaire")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Reference")]
    public string? ReferenceNumber { get; set; }

    [Display(Name = "Numero de lot")]
    public string? LotNumber { get; set; }

    [Display(Name = "Numero de serie")]
    public string? SerialNumber { get; set; }

    [Display(Name = "Date de peremption")]
    [DataType(DataType.Date)]
    public DateOnly? ExpirationDate { get; set; }
}

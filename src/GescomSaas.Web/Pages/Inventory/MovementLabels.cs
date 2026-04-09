using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Inventory;

public static class MovementLabels
{
    public static string Label(StockMovementType type) => type switch
    {
        StockMovementType.OpeningBalance => "Stock initial",
        StockMovementType.Receipt => "Entree",
        StockMovementType.Issue => "Sortie",
        StockMovementType.Transfer => "Transfert",
        StockMovementType.AdjustmentIn => "Ajustement +",
        StockMovementType.AdjustmentOut => "Ajustement -",
        StockMovementType.Reservation => "Reservation",
        StockMovementType.Release => "Liberation",
        _ => type.ToString()
    };
}

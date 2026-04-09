namespace GescomSaas.Application.Models;

public sealed record StockWatchItem(
    string Sku,
    string Label,
    string UnitOfMeasure,
    decimal OnHandQuantity,
    decimal StockValue);

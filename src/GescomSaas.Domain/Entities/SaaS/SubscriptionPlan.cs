using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class SubscriptionPlan : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public TenantEdition Edition { get; set; }
    public decimal MonthlyPrice { get; set; }
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxSuppliers { get; set; }
    public int MaxProducts { get; set; }
    public int MaxWarehouses { get; set; }
    public int MaxMonthlyDocuments { get; set; }
    public decimal OverageUserPrice { get; set; }
    public decimal OverageProductPrice { get; set; }
    public decimal OverageDocumentPrice { get; set; }
    public bool IncludesAdvancedStock { get; set; }
    public bool IncludesPurchasing { get; set; }
    public bool IncludesBusinessIntelligence { get; set; }
}

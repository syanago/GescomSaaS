using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.SaaSAdmin.Plans;

public class PlanInputModel
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Label { get; set; } = string.Empty;

    public TenantEdition Edition { get; set; } = TenantEdition.Essentials;
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

    public static PlanInputModel FromEntity(SubscriptionPlan entity) =>
        new()
        {
            Code = entity.Code,
            Label = entity.Label,
            Edition = entity.Edition,
            MonthlyPrice = entity.MonthlyPrice,
            MaxUsers = entity.MaxUsers,
            MaxCustomers = entity.MaxCustomers,
            MaxSuppliers = entity.MaxSuppliers,
            MaxProducts = entity.MaxProducts,
            MaxWarehouses = entity.MaxWarehouses,
            MaxMonthlyDocuments = entity.MaxMonthlyDocuments,
            OverageUserPrice = entity.OverageUserPrice,
            OverageProductPrice = entity.OverageProductPrice,
            OverageDocumentPrice = entity.OverageDocumentPrice,
            IncludesAdvancedStock = entity.IncludesAdvancedStock,
            IncludesPurchasing = entity.IncludesPurchasing,
            IncludesBusinessIntelligence = entity.IncludesBusinessIntelligence
        };

    public void ApplyTo(SubscriptionPlan entity)
    {
        entity.Code = Code.Trim().ToUpperInvariant();
        entity.Label = Label.Trim();
        entity.Edition = Edition;
        entity.MonthlyPrice = MonthlyPrice;
        entity.MaxUsers = MaxUsers;
        entity.MaxCustomers = MaxCustomers;
        entity.MaxSuppliers = MaxSuppliers;
        entity.MaxProducts = MaxProducts;
        entity.MaxWarehouses = MaxWarehouses;
        entity.MaxMonthlyDocuments = MaxMonthlyDocuments;
        entity.OverageUserPrice = OverageUserPrice;
        entity.OverageProductPrice = OverageProductPrice;
        entity.OverageDocumentPrice = OverageDocumentPrice;
        entity.IncludesAdvancedStock = IncludesAdvancedStock;
        entity.IncludesPurchasing = IncludesPurchasing;
        entity.IncludesBusinessIntelligence = IncludesBusinessIntelligence;
    }
}

using GescomSaas.Domain.Common;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<BusinessPartner> BusinessPartners => Set<BusinessPartner>();
    public DbSet<CommercialDocument> CommercialDocuments => Set<CommercialDocument>();
    public DbSet<CommercialDocumentLine> CommercialDocumentLines => Set<CommercialDocumentLine>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListLine> PriceListLines => Set<PriceListLine>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentLine> StockDocumentLines => Set<StockDocumentLine>();
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();
    public DbSet<PlatformInvoiceLine> PlatformInvoiceLines => Set<PlatformInvoiceLine>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SageImportRun> SageImportRuns => Set<SageImportRun>();
    public DbSet<SageImportRunModule> SageImportRunModules => Set<SageImportRunModule>();
    public DbSet<SageImportProfile> SageImportProfiles => Set<SageImportProfile>();
    public DbSet<SageImportProfileVersion> SageImportProfileVersions => Set<SageImportProfileVersion>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantQuotaNotification> TenantQuotaNotifications => Set<TenantQuotaNotification>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.CompanyLegalName).HasMaxLength(200);
            entity.Property(x => x.Slug).HasMaxLength(80);
            entity.Property(x => x.PrimaryContactEmail).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.AddressLine1).HasMaxLength(160);
            entity.Property(x => x.AddressLine2).HasMaxLength(160);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.City).HasMaxLength(80);
            entity.Property(x => x.State).HasMaxLength(80);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.CashCurrencyCode).HasMaxLength(3);
            entity.Property(x => x.CurrencySymbol).HasMaxLength(8);
            entity.Property(x => x.MoneyDecimalSeparator).HasMaxLength(4);
            entity.Property(x => x.MoneyGroupSeparator).HasMaxLength(4);
            entity.Property(x => x.QuantityDecimalSeparator).HasMaxLength(4);
            entity.Property(x => x.QuantityGroupSeparator).HasMaxLength(4);
            entity.Property(x => x.CountryCode).HasMaxLength(2);
            entity.Property(x => x.SageSqlServerName).HasMaxLength(120);
            entity.Property(x => x.SageSqlDatabaseName).HasMaxLength(120);
            entity.Property(x => x.SageSqlUserName).HasMaxLength(120);
            entity.Property(x => x.SageSqlPassword).HasMaxLength(200);
            entity.Property(x => x.SageCompanyCode).HasMaxLength(80);
        });

        builder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(40);
            entity.Property(x => x.Label).HasMaxLength(120);
            entity.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.OverageUserPrice).HasPrecision(18, 2);
            entity.Property(x => x.OverageProductPrice).HasPrecision(18, 2);
            entity.Property(x => x.OverageDocumentPrice).HasPrecision(18, 2);
        });

        builder.Entity<SageImportRun>(entity =>
        {
            entity.Property(x => x.SourceServer).HasMaxLength(120);
            entity.Property(x => x.SourceDatabase).HasMaxLength(120);
            entity.Property(x => x.WarningSummary).HasMaxLength(2000);
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SageImportProfile>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SageImportProfileVersion>(entity =>
        {
            entity.Property(x => x.Notes).HasMaxLength(600);
            entity.HasIndex(x => new { x.SageImportProfileId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.SageImportProfile)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.SageImportProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SageImportRunModule>(entity =>
        {
            entity.Property(x => x.ModuleName).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.SourceTable).HasMaxLength(240);
            entity.Property(x => x.Summary).HasMaxLength(600);
            entity.Property(x => x.NoteSummary).HasMaxLength(2000);
            entity.HasOne(x => x.SageImportRun)
                .WithMany(x => x.Modules)
                .HasForeignKey(x => x.SageImportRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TenantSubscription>(entity =>
        {
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.TenantId);

            entity.HasOne(x => x.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionPlanId);

            entity.Property(x => x.MonthlyPriceOverride).HasPrecision(18, 2);
        });

        builder.Entity<TenantQuotaNotification>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.QuotaLabel, x.IsResolved });
            entity.Property(x => x.QuotaLabel).HasMaxLength(80);
            entity.Property(x => x.Title).HasMaxLength(180);
            entity.Property(x => x.Message).HasMaxLength(600);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserInvitation>(entity =>
        {
            entity.HasIndex(x => x.InvitationToken).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(80);
            entity.Property(x => x.InvitationToken).HasMaxLength(128);
            entity.Property(x => x.RequestedRoles).HasMaxLength(400);
            entity.Property(x => x.ApplicationUserId).HasMaxLength(450);
            entity.Property(x => x.Notes).HasMaxLength(600);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlatformInvoice>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
            entity.Property(x => x.InvoiceNumber).HasMaxLength(40);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.TotalExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.TotalTax).HasPrecision(18, 2);
            entity.Property(x => x.TotalIncludingTax).HasPrecision(18, 2);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TenantSubscription)
                .WithMany()
                .HasForeignKey(x => x.TenantSubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PlatformInvoiceLine>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPriceExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.LineTotalExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.LineTaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotalIncludingTax).HasPrecision(18, 2);
            entity.HasOne(x => x.PlatformInvoice)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.PlatformInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentTerm>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Label).HasMaxLength(120);
        });

        builder.Entity<TaxCode>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Label).HasMaxLength(120);
            entity.Property(x => x.Rate).HasPrecision(9, 4);
        });

        builder.Entity<ProductCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.Label).HasMaxLength(120);
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
            entity.Property(x => x.Sku).HasMaxLength(50);
            entity.Property(x => x.Label).HasMaxLength(160);
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(10);
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.SalesPrice).HasPrecision(18, 2);
        });

        builder.Entity<PriceList>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.Label).HasMaxLength(120);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
        });

        builder.Entity<PriceListLine>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
        });

        builder.Entity<Warehouse>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(20);
            entity.Property(x => x.Label).HasMaxLength(120);
        });

        builder.Entity<BusinessPartner>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.VatNumber).HasMaxLength(40);
            entity.Property(x => x.CreditLimit).HasPrecision(18, 2);

            entity.OwnsOne(x => x.BillingAddress, address =>
            {
                address.Property(x => x.Recipient).HasMaxLength(120);
                address.Property(x => x.StreetLine1).HasMaxLength(160);
                address.Property(x => x.StreetLine2).HasMaxLength(160);
                address.Property(x => x.PostalCode).HasMaxLength(20);
                address.Property(x => x.City).HasMaxLength(80);
                address.Property(x => x.State).HasMaxLength(80);
                address.Property(x => x.Country).HasMaxLength(80);
            });

            entity.OwnsOne(x => x.ShippingAddress, address =>
            {
                address.Property(x => x.Recipient).HasMaxLength(120);
                address.Property(x => x.StreetLine1).HasMaxLength(160);
                address.Property(x => x.StreetLine2).HasMaxLength(160);
                address.Property(x => x.PostalCode).HasMaxLength(20);
                address.Property(x => x.City).HasMaxLength(80);
                address.Property(x => x.State).HasMaxLength(80);
                address.Property(x => x.Country).HasMaxLength(80);
            });
        });

        builder.Entity<DocumentSequence>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.DocumentType }).IsUnique();
            entity.Property(x => x.Prefix).HasMaxLength(20);
        });

        builder.Entity<CommercialDocument>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            entity.Property(x => x.Number).HasMaxLength(40);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.TotalExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.TotalTax).HasPrecision(18, 2);
            entity.Property(x => x.TotalIncludingTax).HasPrecision(18, 2);

            entity.HasOne(x => x.SourceDocument)
                .WithMany(x => x.DerivedDocuments)
                .HasForeignKey(x => x.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ReferenceNumber });
            entity.Property(x => x.ReferenceNumber).HasMaxLength(50);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Partner)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentAllocation>(entity =>
        {
            entity.Property(x => x.AllocatedAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.PaymentId);
            entity.HasOne(x => x.CommercialDocument)
                .WithMany(x => x.PaymentAllocations)
                .HasForeignKey(x => x.CommercialDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReminderLog>(entity =>
        {
            entity.Property(x => x.Channel).HasMaxLength(30);
            entity.HasOne(x => x.CommercialDocument)
                .WithMany(x => x.ReminderLogs)
                .HasForeignKey(x => x.CommercialDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CommercialDocumentLine>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPriceExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.DiscountRate).HasPrecision(9, 4);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.LineTotalExcludingTax).HasPrecision(18, 2);
            entity.Property(x => x.LineTaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.LineTotalIncludingTax).HasPrecision(18, 2);
            entity.Property(x => x.LotNumber).HasMaxLength(60);
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
        });

        builder.Entity<StockMovement>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(40);
            entity.Property(x => x.LotNumber).HasMaxLength(60);
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
        });

        builder.Entity<StockDocument>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            entity.Property(x => x.Number).HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.SourceWarehouse)
                .WithMany()
                .HasForeignKey(x => x.SourceWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DestinationWarehouse)
                .WithMany()
                .HasForeignKey(x => x.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockDocumentLine>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.LotNumber).HasMaxLength(60);
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
            entity.HasOne(x => x.StockDocument)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.StockDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(80);
            entity.Property(x => x.LastName).HasMaxLength(80);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOnUtc = DateTime.UtcNow;
            }

            if (entry.State is EntityState.Modified or EntityState.Added)
            {
                entry.Entity.UpdatedOnUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

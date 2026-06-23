using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Services;

/// <summary>
/// Tests reels de TenantQuotaEnforcementService avec EF Core InMemory.
/// Verifient que les quotas levent QuotaExceededException avec le bon
/// nom, la bonne limite et le bon comptage actuel.
/// </summary>
public class QuotaEnforcementTests : IAsyncLifetime
{
    private ApplicationDbContext _db = null!;
    private TenantQuotaEnforcementService _sut = null!;

    private Guid _tenantId;
    private Guid _planId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"quota-tests-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options);
        _sut = new TenantQuotaEnforcementService(_db);

        _tenantId = Guid.NewGuid();
        _planId = Guid.NewGuid();

        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            CompanyName = "Test Co",
            Slug = "test-co",
        });

        _db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = _planId,
            Code = "starter",
            Label = "Starter",
            MaxUsers = 2,
            MaxCustomers = 50,
            MaxSuppliers = 50,
            MaxProducts = 100,
            MaxWarehouses = 1,
            MaxMonthlyDocuments = 100,
        });

        _db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = _tenantId,
            SubscriptionPlanId = _planId,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = SubscriptionStatus.Active,
        });

        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EnsureCanManageUsers_QuandLimiteAtteinte_LeveQuotaExceededException()
    {
        // Arrange : on remplit le tenant avec 2 utilisateurs (= MaxUsers du plan Starter)
        for (var i = 0; i < 2; i++)
        {
            _db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"user{i}@test.com",
                Email = $"user{i}@test.com",
                TenantId = _tenantId,
            });
        }
        await _db.SaveChangesAsync();

        // Act : tentative d'ajout d'un 3eme utilisateur
        var act = async () => await _sut.EnsureCanManageUsersAsync(_tenantId, additionalUsers: 1);

        // Assert : QuotaExceededException avec metadonnees structurees
        var ex = await act.Should().ThrowAsync<QuotaExceededException>();
        ex.Which.QuotaName.Should().Be("utilisateurs");
        ex.Which.Limit.Should().Be(2);
        ex.Which.Current.Should().Be(2);
        ex.Which.HttpStatusCode.Should().Be(402);
        ex.Which.ErrorCode.Should().Be("QUOTA_EXCEEDED");
        ex.Which.Data["planLabel"].Should().Be("Starter");
    }

    [Fact]
    public async Task EnsureCanManageUsers_QuandLimiteNonAtteinte_NeLevePasException()
    {
        // Arrange : 1 utilisateur pour un plan a 2
        _db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "single@test.com",
            Email = "single@test.com",
            TenantId = _tenantId,
        });
        await _db.SaveChangesAsync();

        // Act + Assert : aucune exception pour ajouter le 2eme
        var act = async () => await _sut.EnsureCanManageUsersAsync(_tenantId, additionalUsers: 1);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanManageUsers_QuandTenantInexistant_LeveNotFoundException()
    {
        var inexistantId = Guid.NewGuid();

        var act = async () => await _sut.EnsureCanManageUsersAsync(inexistantId, additionalUsers: 1);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be("Tenant");
        ex.Which.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task EnsureCanManageUsers_AvecOverridePlanSuperieur_RespecteLOverride()
    {
        // Arrange : on monte la limite via override (sans toucher au plan)
        var subscription = await _db.TenantSubscriptions.FirstAsync(x => x.TenantId == _tenantId);
        subscription.MaxUsersOverride = 5;
        await _db.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            _db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = $"u{i}@test.com",
                Email = $"u{i}@test.com",
                TenantId = _tenantId,
            });
        }
        await _db.SaveChangesAsync();

        // Act : 3 users + 2 nouveaux = 5 (= override) -> OK
        var actOk = async () => await _sut.EnsureCanManageUsersAsync(_tenantId, additionalUsers: 2);
        await actOk.Should().NotThrowAsync();

        // Mais 3 + 3 = 6 > 5 -> exception avec limit=5 (l'override, pas le plan)
        var actFail = async () => await _sut.EnsureCanManageUsersAsync(_tenantId, additionalUsers: 3);
        var ex = await actFail.Should().ThrowAsync<QuotaExceededException>();
        ex.Which.Limit.Should().Be(5);
    }
}

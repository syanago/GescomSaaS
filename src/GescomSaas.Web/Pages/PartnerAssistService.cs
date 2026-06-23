using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages;

public static class PartnerAssistService
{
    public static async Task<(Tenant Tenant, IReadOnlyList<PartnerLookupOption> Options)> LoadOptionsAsync(
        ApplicationDbContext dbContext,
        Guid tenantId,
        IReadOnlyCollection<BusinessPartnerType> allowedTypes,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        var partners = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && allowedTypes.Contains(x.PartnerType))
            .OrderBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name
            })
            .ToListAsync(cancellationToken);

        var options = partners
            .Select(x => new PartnerLookupOption(
                x.Id,
                x.Code,
                x.Name,
                FormatLookupValue(tenant.PartnerLookupMode, x.Code, x.Name),
                $"{x.Code} - {x.Name}"))
            .ToList();

        return (tenant, options);
    }

    public static string FormatLookupValue(PartnerLookupMode mode, string code, string name) =>
        mode == PartnerLookupMode.Name ? name : code;

    public static async Task<PartnerAssistResult> ResolveOrCreateAsync(
        ApplicationDbContext dbContext,
        INumberingService numberingService,
        Guid tenantId,
        IReadOnlyCollection<BusinessPartnerType> allowedTypes,
        BusinessPartnerType newPartnerType,
        ReferenceNumberingScope numberingScope,
        PartnerLookupMode lookupMode,
        AssistedPartnerEntryInputModel partnerEntry,
        CancellationToken cancellationToken = default)
    {
        var lookup = partnerEntry.Lookup?.Trim();
        if (string.IsNullOrWhiteSpace(lookup))
        {
            return new PartnerAssistResult(null, null, false, null);
        }

        var candidates = await dbContext.BusinessPartners
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && allowedTypes.Contains(x.PartnerType))
            .Where(x => lookupMode == PartnerLookupMode.Code
                ? x.Code == lookup
                : x.Name == lookup)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 1)
        {
            var match = candidates[0];
            return new PartnerAssistResult(
                match.Id,
                FormatLookupValue(lookupMode, match.Code, match.Name),
                false,
                null);
        }

        if (candidates.Count > 1)
        {
            return new PartnerAssistResult(
                null,
                lookup,
                false,
                lookupMode == PartnerLookupMode.Name
                    ? "Plusieurs tiers portent ce nom. Renseigne le code du tiers ou affine la designation."
                    : "Plusieurs tiers correspondent a cette saisie.");
        }

        if (!partnerEntry.CreateIfMissing)
        {
            return new PartnerAssistResult(
                null,
                lookup,
                false,
                lookupMode == PartnerLookupMode.Name
                    ? "Aucun tiers actif ne correspond a ce nom."
                    : "Aucun tiers actif ne correspond a ce code.");
        }

        var newName = string.IsNullOrWhiteSpace(partnerEntry.NewName)
            ? (lookupMode == PartnerLookupMode.Name ? lookup : string.Empty)
            : partnerEntry.NewName.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            return new PartnerAssistResult(null, lookup, false, "Renseigne le nom du nouveau tiers.");
        }

        var requestedCode = string.IsNullOrWhiteSpace(partnerEntry.NewCode)
            ? (lookupMode == PartnerLookupMode.Code ? lookup : null)
            : partnerEntry.NewCode.Trim();

        string resolvedCode;
        try
        {
            resolvedCode = await numberingService.ResolveReferenceCodeAsync(
                tenantId,
                numberingScope,
                requestedCode,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return new PartnerAssistResult(null, lookup, false, exception.Message);
        }

        var partner = new BusinessPartner
        {
            TenantId = tenantId,
            Code = resolvedCode,
            Name = newName,
            PartnerType = newPartnerType,
            Email = string.IsNullOrWhiteSpace(partnerEntry.Email) ? null : partnerEntry.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(partnerEntry.PhoneNumber) ? null : partnerEntry.PhoneNumber.Trim(),
            IsActive = true
        };

        dbContext.BusinessPartners.Add(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PartnerAssistResult(
            partner.Id,
            FormatLookupValue(lookupMode, partner.Code, partner.Name),
            true,
            null);
    }
}

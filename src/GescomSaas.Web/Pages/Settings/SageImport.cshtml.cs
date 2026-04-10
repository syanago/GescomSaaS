using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;
using System.Text.Json;

namespace GescomSaas.Web.Pages.Settings;

[Authorize(Roles = "TenantOwner,PlatformAdmin")]
public class SageImportModel(
    ISageImportService sageImportService,
    ApplicationDbContext dbContext,
    GescomSaas.Application.Contracts.ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    private static readonly JsonSerializerOptions ComparisonJsonOptions = new(JsonSerializerDefaults.Web);

    [BindProperty]
    public SageImportInputModel Input { get; set; } = new();

    [BindProperty]
    public string TransferProfileJson { get; set; } = string.Empty;

    [BindProperty]
    public IFormFile? TransferProfileFile { get; set; }

    [BindProperty]
    public Guid? SelectedSavedProfileId { get; set; }

    [BindProperty]
    public string SavedProfileName { get; set; } = string.Empty;

    [BindProperty]
    public string SavedProfileDescription { get; set; } = string.Empty;

    [BindProperty]
    public string SavedProfileVersionNotes { get; set; } = string.Empty;

    [BindProperty]
    public bool SaveAsDefaultProfile { get; set; }

    public SageConnectionReport? ConnectionReport { get; private set; }
    public SageSchemaAnalysisReport? SchemaAnalysisReport { get; private set; }
    public SageImportExecutionReport? ExecutionReport { get; private set; }
    public SageImportPreviewReport? PreviewReport { get; private set; }
    public SageImportComparisonReport? ComparisonReport { get; private set; }
    public IReadOnlyList<SageImportHistoryItem> RecentRuns { get; private set; } = [];
    public IReadOnlyList<SageImportProfileLibraryItem> SavedProfiles { get; private set; } = [];
    public IReadOnlyList<SageSchemaSelectableTable> AvailableSchemaTables { get; private set; } = [];
    public string AvailableSchemaJson => JsonSerializer.Serialize(AvailableSchemaTables);

    public IReadOnlyList<SelectListItem> AuthenticationModes { get; } =
    [
        new("Windows integree", ExternalSqlAuthenticationMode.Windows.ToString()),
        new("SQL Server", ExternalSqlAuthenticationMode.SqlServer.ToString())
    ];

    public IReadOnlyList<SelectListItem> ImportModes { get; } =
    [
        new("Complet", SageImportMode.Complete.ToString()),
        new("Partiel cible", SageImportMode.Partial.ToString()),
        new("Delta incremental", SageImportMode.Delta.ToString())
    ];

    public IReadOnlyList<SelectListItem> ExistingRecordPolicies { get; } =
    [
        new("Mettre a jour par code", SageExistingRecordPolicy.UpdateByCode.ToString()),
        new("Ignorer l'existant", SageExistingRecordPolicy.SkipExisting.ToString()),
        new("Dupliquer avec prefixe", SageExistingRecordPolicy.PrefixAndCreate.ToString())
    ];

    public IReadOnlyList<SelectListItem> MissingReferencePolicies { get; } =
    [
        new("Creer les references manquantes", SageMissingReferencePolicy.CreateMissing.ToString()),
        new("Ignorer les enregistrements dependants", SageMissingReferencePolicy.SkipDependentRecords.ToString()),
        new("Bloquer le transfert", SageMissingReferencePolicy.BlockTransfer.ToString())
    ];

    public IReadOnlyList<SelectListItem> DocumentNumberPolicies { get; } =
    [
        new("Conserver le numero Sage", SageDocumentNumberPolicy.PreserveSourceNumber.ToString()),
        new("Renumeroter dans LigCom", SageDocumentNumberPolicy.RenumberInLigCom.ToString()),
        new("Conserver puis prefixer en doublon", SageDocumentNumberPolicy.PreserveWithPrefixIfDuplicate.ToString())
    ];

    public IReadOnlyList<string> SelectedModules => Input.SelectedModules();
    public IReadOnlyList<string> ActiveFilters => Input.ActiveFilters();
    public IReadOnlyList<string> ExecutionHighlights => Input.ExecutionHighlights();

    public async Task<IActionResult> OnGetAsync()
    {
        var tenant = await LoadTenantAsync();
        Input = SageImportInputModel.FromEntity(tenant);
        TransferProfileJson = Input.ToPortableJson();
        RecentRuns = await LoadRecentRunsAsync(tenant.Id);
        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenant = await LoadTenantAsync();

        ValidateForSave();
        if (!ModelState.IsValid)
        {
            await TryLoadSchemaMetadataAsync();
            return Page();
        }

        Input.ApplyTo(tenant);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        TransferProfileJson = Input.ToPortableJson();

        StatusMessage = "Profil de transfert Sage SQL enregistre. Il est pret pour un import complet, partiel ou pilote par simulation.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateProfileJsonAsync()
    {
        TransferProfileJson = Input.ToPortableJson();
        SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = "Le profil JSON exportable a ete genere. Tu peux le copier ou le reutiliser sur un autre dossier Sage.";
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadProfileJsonAsync()
    {
        var tenant = await LoadTenantAsync();
        var payload = Input.ToPortableJson();
        var tenantSlug = string.IsNullOrWhiteSpace(tenant.Slug) ? "tenant" : tenant.Slug.Trim().ToLowerInvariant();
        var fileName = $"ligcom-sage-profile-{tenantSlug}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        return File(System.Text.Encoding.UTF8.GetBytes(payload), "application/json", fileName);
    }

    public async Task<IActionResult> OnPostImportProfileJsonAsync()
    {
        if (!SageImportInputModel.TryParsePortableJson(TransferProfileJson, out var importedProfile, out var error))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
            ModelState.AddModelError(string.Empty, $"Le profil JSON n'a pas pu etre charge : {error}");
            return Page();
        }

        Input = importedProfile;
        TransferProfileJson = Input.ToPortableJson();
        SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = "Le profil JSON a ete charge dans l'ecran. Controle-le puis enregistre pour l'appliquer au tenant courant.";
        return Page();
    }

    public async Task<IActionResult> OnPostImportProfileFileAsync()
    {
        if (TransferProfileFile is null || TransferProfileFile.Length == 0)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
            ModelState.AddModelError(string.Empty, "Selectionne un fichier JSON avant de lancer l'import du profil.");
            return Page();
        }

        using var reader = new StreamReader(TransferProfileFile.OpenReadStream());
        TransferProfileJson = await reader.ReadToEndAsync();

        if (!SageImportInputModel.TryParsePortableJson(TransferProfileJson, out var importedProfile, out var error))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
            ModelState.AddModelError(string.Empty, $"Le fichier JSON n'a pas pu etre charge : {error}");
            return Page();
        }

        Input = importedProfile;
        TransferProfileJson = Input.ToPortableJson();
        SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil a ete charge depuis le fichier {TransferProfileFile.FileName}. Controle-le puis enregistre pour l'appliquer au tenant courant.";
        return Page();
    }

    public async Task<IActionResult> OnPostSaveStoredProfileAsync()
    {
        var tenant = await LoadTenantAsync();

        if (string.IsNullOrWhiteSpace(SavedProfileName))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Renseigne un nom de profil avant de l'enregistrer.");
            return Page();
        }

        var profile = SelectedSavedProfileId.HasValue
            ? await DbContext.SageImportProfiles.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == SelectedSavedProfileId.Value && x.TenantId == tenant.Id, HttpContext.RequestAborted)
            : null;

        if (profile is null)
        {
            profile = new GescomSaas.Domain.Entities.SaaS.SageImportProfile
            {
                TenantId = tenant.Id,
                Name = SavedProfileName.Trim()
            };
            DbContext.SageImportProfiles.Add(profile);
        }

        profile.Name = SavedProfileName.Trim();
        profile.Description = SavedProfileDescription.Trim();
        profile.IsDefault = SaveAsDefaultProfile;
        profile.IsArchived = false;
        profile.UpdatedOnUtc = DateTime.UtcNow;

        if (SaveAsDefaultProfile)
        {
            var otherProfiles = await DbContext.SageImportProfiles
                .Where(x => x.TenantId == tenant.Id && x.Id != profile.Id && x.IsDefault)
                .ToListAsync(HttpContext.RequestAborted);
            foreach (var other in otherProfiles)
            {
                other.IsDefault = false;
                other.UpdatedOnUtc = DateTime.UtcNow;
            }
        }

        var nextVersion = profile.Versions.Count > 0 ? profile.Versions.Max(x => x.VersionNumber) + 1 : 1;
        DbContext.SageImportProfileVersions.Add(new GescomSaas.Domain.Entities.SaaS.SageImportProfileVersion
        {
            TenantId = tenant.Id,
            SageImportProfile = profile,
            VersionNumber = nextVersion,
            Notes = SavedProfileVersionNotes.Trim(),
            ProfileJson = Input.ToPortableJson()
        });

        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        SelectedSavedProfileId = profile.Id;
        SavedProfileVersionNotes = string.Empty;
        TransferProfileJson = Input.ToPortableJson();
        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {profile.Name} a ete enregistre en version {nextVersion}.";
        return Page();
    }

    public async Task<IActionResult> OnPostArchiveStoredProfileAsync(Guid profileId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles.FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);
        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a archiver est introuvable.");
            return Page();
        }

        profile.IsArchived = true;
        profile.IsDefault = false;
        profile.UpdatedOnUtc = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        if (SelectedSavedProfileId == profile.Id)
        {
            SelectedSavedProfileId = null;
            SavedProfileName = string.Empty;
            SavedProfileDescription = string.Empty;
            SavedProfileVersionNotes = string.Empty;
            SaveAsDefaultProfile = false;
        }

        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {profile.Name} a ete archive.";
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreStoredProfileAsync(Guid profileId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles.FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);
        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a restaurer est introuvable.");
            return Page();
        }

        profile.IsArchived = false;
        profile.UpdatedOnUtc = DateTime.UtcNow;
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {profile.Name} a ete restaure.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteStoredProfileAsync(Guid profileId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);
        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a supprimer est introuvable.");
            return Page();
        }

        if (!profile.IsArchived)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Archive d'abord le profil avant de le supprimer definitivement.");
            return Page();
        }

        DbContext.SageImportProfiles.Remove(profile);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        if (SelectedSavedProfileId == profile.Id)
        {
            SelectedSavedProfileId = null;
            SavedProfileName = string.Empty;
            SavedProfileDescription = string.Empty;
            SavedProfileVersionNotes = string.Empty;
            SaveAsDefaultProfile = false;
        }

        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {profile.Name} a ete supprime definitivement.";
        return Page();
    }

    public async Task<IActionResult> OnPostDuplicateStoredProfileAsync(Guid profileId)
    {
        var tenant = await LoadTenantAsync();
        var sourceProfile = await DbContext.SageImportProfiles
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);

        if (sourceProfile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a dupliquer est introuvable.");
            return Page();
        }

        var sourceVersion = sourceProfile.Versions
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();

        var error = string.Empty;
        if (sourceVersion is null || !SageImportInputModel.TryParsePortableJson(sourceVersion.ProfileJson, out var duplicatedProfileInput, out error))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, $"Le profil a dupliquer ne contient pas de version exploitable : {(string.IsNullOrWhiteSpace(error) ? "aucune version disponible." : error)}");
            return Page();
        }

        var duplicateName = await BuildDuplicateProfileNameAsync(tenant.Id, sourceProfile.Name);
        var duplicateDescription = string.IsNullOrWhiteSpace(sourceProfile.Description)
            ? $"Variante du profil {sourceProfile.Name}"
            : $"{sourceProfile.Description} - Variante";

        var duplicateProfile = new GescomSaas.Domain.Entities.SaaS.SageImportProfile
        {
            TenantId = tenant.Id,
            Name = duplicateName,
            Description = duplicateDescription,
            IsDefault = false,
            IsArchived = false,
            UpdatedOnUtc = DateTime.UtcNow
        };

        DbContext.SageImportProfiles.Add(duplicateProfile);
        DbContext.SageImportProfileVersions.Add(new GescomSaas.Domain.Entities.SaaS.SageImportProfileVersion
        {
            TenantId = tenant.Id,
            SageImportProfile = duplicateProfile,
            VersionNumber = 1,
            Notes = $"Copie initiale depuis {sourceProfile.Name} v{sourceVersion.VersionNumber}",
            ProfileJson = sourceVersion.ProfileJson
        });

        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        Input = duplicatedProfileInput;
        TransferProfileJson = Input.ToPortableJson();
        SelectedSavedProfileId = duplicateProfile.Id;
        SavedProfileName = duplicateProfile.Name;
        SavedProfileDescription = duplicateProfile.Description;
        SavedProfileVersionNotes = string.Empty;
        SaveAsDefaultProfile = false;
        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {sourceProfile.Name} a ete duplique en {duplicateProfile.Name}. Tu peux maintenant l'ajuster sans repartir de zero.";
        return Page();
    }

    public async Task<IActionResult> OnPostLoadStoredProfileAsync(Guid profileId, Guid? versionId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);

        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil demande est introuvable.");
            return Page();
        }

        var version = versionId.HasValue
            ? profile.Versions.FirstOrDefault(x => x.Id == versionId.Value)
            : profile.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();

        var error = string.Empty;
        if (version is null || !SageImportInputModel.TryParsePortableJson(version.ProfileJson, out var importedProfile, out error))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, $"Le profil stocke n'a pas pu etre charge : {(string.IsNullOrWhiteSpace(error) ? "aucune version exploitable n'a ete trouvee." : error)}");
            return Page();
        }

        Input = importedProfile;
        TransferProfileJson = Input.ToPortableJson();
        SelectedSavedProfileId = profile.Id;
        SavedProfileName = profile.Name;
        SavedProfileDescription = profile.Description;
        SaveAsDefaultProfile = profile.IsDefault;
        SavedProfileVersionNotes = string.Empty;
        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = $"Le profil {profile.Name} version {version.VersionNumber} a ete charge dans l'ecran.";
        return Page();
    }

    public async Task<IActionResult> OnPostCompareStoredProfileAsync(Guid profileId, Guid? versionId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);

        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a comparer est introuvable.");
            return Page();
        }

        var version = versionId.HasValue
            ? profile.Versions.FirstOrDefault(x => x.Id == versionId.Value)
            : profile.Versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();

        var error = string.Empty;
        if (version is null || !SageImportInputModel.TryParsePortableJson(version.ProfileJson, out var comparedProfile, out error))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, $"Le profil compare n'a pas pu etre charge : {(string.IsNullOrWhiteSpace(error) ? "aucune version exploitable n'a ete trouvee." : error)}");
            return Page();
        }

        ComparisonReport = BuildComparisonReport(
            "Configuration en cours",
            $"{profile.Name} v{version.VersionNumber}",
            Input,
            comparedProfile);

        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = ComparisonReport.DifferenceCount == 0
            ? $"Aucun ecart detecte entre l'ecran courant et {profile.Name} v{version.VersionNumber}."
            : $"{ComparisonReport.DifferenceCount} ecart(s) detecte(s) entre l'ecran courant et {profile.Name} v{version.VersionNumber}.";
        return Page();
    }

    public async Task<IActionResult> OnPostCompareStoredProfileVersionsAsync(Guid profileId, Guid? leftVersionId, Guid? rightVersionId)
    {
        var tenant = await LoadTenantAsync();
        var profile = await DbContext.SageImportProfiles
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenant.Id, HttpContext.RequestAborted);

        if (profile is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Le profil a comparer est introuvable.");
            return Page();
        }

        var orderedVersions = profile.Versions
            .OrderByDescending(x => x.VersionNumber)
            .ToList();

        GescomSaas.Domain.Entities.SaaS.SageImportProfileVersion? leftVersion;
        GescomSaas.Domain.Entities.SaaS.SageImportProfileVersion? rightVersion;

        if (leftVersionId.HasValue && rightVersionId.HasValue)
        {
            leftVersion = orderedVersions.FirstOrDefault(x => x.Id == leftVersionId.Value);
            rightVersion = orderedVersions.FirstOrDefault(x => x.Id == rightVersionId.Value);
        }
        else
        {
            leftVersion = orderedVersions.ElementAtOrDefault(0);
            rightVersion = orderedVersions.ElementAtOrDefault(1);
        }

        if (leftVersion is null || rightVersion is null)
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            ModelState.AddModelError(string.Empty, "Il faut au moins deux versions exploitables pour lancer une comparaison directe.");
            return Page();
        }

        var leftError = string.Empty;
        var rightError = string.Empty;
        if (!TryReadProfileVersion(leftVersion, out var leftProfile, out leftError) ||
            !TryReadProfileVersion(rightVersion, out var rightProfile, out rightError))
        {
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            var combinedError = string.Join(" ", new[] { leftError, rightError }.Where(x => !string.IsNullOrWhiteSpace(x)));
            ModelState.AddModelError(string.Empty, $"La comparaison des versions n'a pas pu etre preparee : {combinedError}");
            return Page();
        }

        ComparisonReport = BuildComparisonReport(
            $"{profile.Name} v{leftVersion.VersionNumber}",
            $"{profile.Name} v{rightVersion.VersionNumber}",
            leftProfile,
            rightProfile);

        SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
        await TryLoadSchemaMetadataAsync();
        StatusMessage = ComparisonReport.DifferenceCount == 0
            ? $"Aucun ecart detecte entre {profile.Name} v{leftVersion.VersionNumber} et v{rightVersion.VersionNumber}."
            : $"{ComparisonReport.DifferenceCount} ecart(s) detecte(s) entre {profile.Name} v{leftVersion.VersionNumber} et v{rightVersion.VersionNumber}.";
        return Page();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        var tenant = await LoadTenantAsync();
        ValidateConnection();
        if (!ModelState.IsValid)
        {
            ConnectionReport = new SageConnectionReport
            {
                Success = false,
                Message = "La connexion n'a pas ete testee car certains champs source sont incomplets."
            };
            await TryLoadSchemaMetadataAsync();
            SavedProfiles = await LoadSavedProfilesAsync(tenant.Id);
            return Page();
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(HttpContext.RequestAborted);

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
                    DB_NAME() AS DatabaseName,
                    SUSER_SNAME() AS LoginName,
                    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
                    (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) AS UserTableCount;
                """;

            string serverName = string.Empty;
            string databaseName = string.Empty;
            string loginName = string.Empty;
            string productVersion = string.Empty;
            var tableCount = 0;

            await using (var reader = await command.ExecuteReaderAsync(HttpContext.RequestAborted))
            {
                if (await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    serverName = reader.GetString(0);
                    databaseName = reader.GetString(1);
                    loginName = reader.GetString(2);
                    productVersion = reader.GetString(3);
                    tableCount = reader.GetInt32(4);
                }
            }

            List<string> sampleTables = [];
            var tableCommand = connection.CreateCommand();
            tableCommand.CommandText =
                """
                SELECT TOP (8) [name]
                FROM sys.tables
                WHERE is_ms_shipped = 0
                ORDER BY [name];
                """;

            await using (var tableReader = await tableCommand.ExecuteReaderAsync(HttpContext.RequestAborted))
            {
                while (await tableReader.ReadAsync(HttpContext.RequestAborted))
                {
                    sampleTables.Add(tableReader.GetString(0));
                }
            }

            ConnectionReport = new SageConnectionReport
            {
                Success = true,
                Message = "Connexion SQL et lecture du catalogue reussies.",
                ServerName = serverName,
                DatabaseName = databaseName,
                LoginName = loginName,
                SqlVersion = productVersion,
                TableCount = tableCount,
                SampleTables = sampleTables
            };

            StatusMessage = "Connexion Sage SQL reussie. Le profil peut maintenant etre sauvegarde ou affine.";
        }
        catch (Exception ex)
        {
            ConnectionReport = new SageConnectionReport
            {
                Success = false,
                Message = ex.Message
            };

            ModelState.AddModelError(string.Empty, "La connexion Sage SQL a echoue. Verifie le serveur, la base et les identifiants.");
        }

        await TryLoadSchemaMetadataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAnalyzeSchemaAsync()
    {
        ValidateConnection();
        if (!ModelState.IsValid)
        {
            SchemaAnalysisReport = new SageSchemaAnalysisReport
            {
                Success = false,
                Message = "L'analyse du schema n'a pas demarre car la connexion source est incomplete."
            };
            await TryLoadSchemaMetadataAsync();
            return Page();
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(HttpContext.RequestAborted);

            var tables = await ReadSchemaAsync(connection);
            var keyTables = tables
                .OrderByDescending(x => x.Columns.Count)
                .ThenBy(x => x.TableName)
                .Take(8)
                .Select(x => new SageSchemaTableProfile
                {
                    TableName = x.TableName,
                    ColumnCount = x.Columns.Count,
                    SampleColumns = x.Columns.Take(8).ToArray()
                })
                .ToArray();

            SchemaAnalysisReport = new SageSchemaAnalysisReport
            {
                Success = true,
                Message = "Lecture du schema source reussie. Les propositions ci-dessous sont deduites automatiquement a partir des noms de tables et de colonnes detectes.",
                InferenceNote = "Inference LigCom : ces correspondances sont proposees a partir du dossier Sage connecte et doivent etre validees avant l'import reel.",
                TableCount = tables.Count,
                KeyTables = keyTables,
                Suggestions = BuildMappingSuggestions(tables, Input.SchemaMapping)
            };

            StatusMessage = "Schema Sage lu avec succes. Les suggestions de mapping technique sont disponibles plus bas.";
        }
        catch (Exception ex)
        {
            SchemaAnalysisReport = new SageSchemaAnalysisReport
            {
                Success = false,
                Message = ex.Message
            };
            ModelState.AddModelError(string.Empty, "L'analyse du schema Sage a echoue. Teste d'abord la connexion puis relance l'analyse.");
        }

        await TryLoadSchemaMetadataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostApplySuggestedMappingAsync()
    {
        ValidateConnection();
        if (!ModelState.IsValid)
        {
            await TryLoadSchemaMetadataAsync();
            ModelState.AddModelError(string.Empty, "Le mapping automatique n'a pas demarre car la connexion Sage est incomplete.");
            return Page();
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(HttpContext.RequestAborted);
            var tables = await ReadSchemaAsync(connection);

            AvailableSchemaTables = tables
                .OrderBy(x => x.TableName)
                .Select(x => new SageSchemaSelectableTable
                {
                    TableName = x.TableName,
                    Columns = x.Columns.OrderBy(column => column).ToArray()
                })
                .ToArray();

            ApplySuggestedModuleMapping(Input.SchemaMapping.Partners, tables,
                ["F_COMPTET", "CLIENT", "CLIENTS", "FOURNISSEUR", "FOURNISSEURS", "F_CLIENT", "F_FOURNISSEUR"],
                ["CT_NUM", "CT_INTITULE", "CT_EMAIL", "CT_TELEPHONE", "CT_CONTACT"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.ProductCategories, tables,
                ["F_FAMILLE", "FAMILLE", "F_ARTFAM"],
                ["FA_CODEFAMILLE", "FA_INTITULE", "FA_LIBELLE"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.TaxCodes, tables,
                ["F_TAXE", "TAXE", "TAXES", "TAUXTVA"],
                ["TA_CODE", "TA_TAUX", "TA_INTITULE"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.PaymentTerms, tables,
                ["F_REGLEMENT", "P_REGLEMENT", "REGLEMENT"],
                ["RG_CODE", "RG_LIBELLE", "RG_NBJOUR"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.Warehouses, tables,
                ["F_DEPOT", "DEPOT", "DEPOTS", "MAGASIN"],
                ["DE_NO", "DE_INTITULE", "DE_LIBELLE"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.Products, tables,
                ["F_ARTICLE", "ARTICLE", "ARTICLES"],
                ["AR_REF", "AR_DESIGN", "AR_PRIXVEN", "AR_PRIXACH", "FA_CODEFAMILLE"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.PriceLists, tables,
                ["F_TARIF", "TARIF", "TARIFS", "ARTTARIF"],
                ["AR_REF", "CT_NUM", "PRIX", "TARIF"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.Stock, tables,
                ["F_STOCK", "STOCK", "MOUVSTOCK", "F_MOUVSTOCK", "MOUVEMENTSTOCK"],
                ["AR_REF", "DE_NO", "QTE", "COUT", "CMP", "LOT", "SERIE"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.DocumentHeaders, tables,
                ["F_DOCENTETE", "DOCENTETE", "F_DOCUMENT", "DOCUMENT"],
                ["DO_PIECE", "DO_DATE", "DO_TIERS", "DO_TYPE", "DE_NO"]);
            ApplySuggestedModuleMapping(Input.SchemaMapping.DocumentLines, tables,
                ["F_DOCLIGNE", "DOCLIGNE", "F_DOCLIG", "DOCUMENTLIGNE"],
                ["DO_PIECE", "AR_REF", "DL_QTE", "DL_PUHT", "DL_PUTTC", "LOT", "SERIE"]);

            SchemaAnalysisReport = new SageSchemaAnalysisReport
            {
                Success = true,
                Message = "Le schema Sage a ete relu et le mapping propose a ete applique dans le profil.",
                InferenceNote = "Les tables et colonnes ont ete choisies automatiquement. Verifie les points sensibles avant l'import reel.",
                TableCount = tables.Count,
                KeyTables = tables
                    .OrderByDescending(x => x.Columns.Count)
                    .ThenBy(x => x.TableName)
                    .Take(8)
                    .Select(x => new SageSchemaTableProfile
                    {
                        TableName = x.TableName,
                        ColumnCount = x.Columns.Count,
                        SampleColumns = x.Columns.Take(8).ToArray()
                    })
                    .ToArray(),
                Suggestions = BuildMappingSuggestions(tables, Input.SchemaMapping)
            };

            StatusMessage = "Le mapping suggere a ete applique. Tu peux controler les champs proposes avant d'enregistrer le profil.";
        }
        catch (Exception ex)
        {
            await TryLoadSchemaMetadataAsync();
            ModelState.AddModelError(string.Empty, $"Le mapping automatique a echoue : {ex.Message}");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPreviewDataAsync()
    {
        ValidateForSave();
        if (!ModelState.IsValid)
        {
            PreviewReport = new SageImportPreviewReport
            {
                Success = false,
                Message = "La previsualisation n'a pas demarre car le profil source est incomplet."
            };
            await TryLoadSchemaMetadataAsync();
            return Page();
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(HttpContext.RequestAborted);
            var schema = await ReadSchemaAsync(connection);

            List<SageImportPreviewModule> modules = [];

            await AddPreviewAsync(modules, connection, schema, "Tiers", Input.Scope.ImportCustomers || Input.Scope.ImportSuppliers,
                ["F_COMPTET", "CLIENT", "CLIENTS", "FOURNISSEUR", "FOURNISSEURS", "F_CLIENT", "F_FOURNISSEUR"],
                ["CT_NUM", "CT_INTITULE", "CT_EMAIL", "CT_TELEPHONE"],
                Input.SchemaMapping.Partners.TableName);

            await AddPreviewAsync(modules, connection, schema, "Familles", Input.Scope.ImportProductCategories,
                ["F_FAMILLE", "FAMILLE", "F_ARTFAM"],
                ["FA_CODEFAMILLE", "FA_INTITULE", "FA_LIBELLE"],
                Input.SchemaMapping.ProductCategories.TableName);

            await AddPreviewAsync(modules, connection, schema, "Taxes", Input.Scope.ImportTaxCodes,
                ["F_TAXE", "TAXE", "TAXES", "TAUXTVA"],
                ["TA_CODE", "TA_TAUX", "TA_INTITULE"],
                Input.SchemaMapping.TaxCodes.TableName);

            await AddPreviewAsync(modules, connection, schema, "Conditions de paiement", Input.Scope.ImportPaymentTerms,
                ["F_REGLEMENT", "P_REGLEMENT", "REGLEMENT"],
                ["RG_CODE", "RG_LIBELLE", "RG_NBJOUR"],
                Input.SchemaMapping.PaymentTerms.TableName);

            await AddPreviewAsync(modules, connection, schema, "Depots", Input.Scope.ImportWarehouses,
                ["F_DEPOT", "DEPOT", "DEPOTS", "MAGASIN"],
                ["DE_NO", "DE_INTITULE", "DE_LIBELLE"],
                Input.SchemaMapping.Warehouses.TableName);

            await AddPreviewAsync(modules, connection, schema, "Articles", Input.Scope.ImportProducts,
                ["F_ARTICLE", "ARTICLE", "ARTICLES"],
                ["AR_REF", "AR_DESIGN", "AR_PRIXVEN", "AR_PRIXACH"],
                Input.SchemaMapping.Products.TableName);

            await AddPreviewAsync(modules, connection, schema, "Listes de prix", Input.Scope.ImportPriceLists,
                ["F_TARIF", "TARIF", "TARIFS", "ARTTARIF"],
                ["AR_REF", "PRIX", "TARIF"],
                Input.SchemaMapping.PriceLists.TableName);

            await AddPreviewAsync(modules, connection, schema, "Stock initial", Input.Scope.ImportOpeningStock,
                ["F_STOCK", "STOCK", "MOUVSTOCK", "F_MOUVSTOCK", "MOUVEMENTSTOCK"],
                ["AR_REF", "DE_NO", "QTE", "COUT", "CMP"],
                Input.SchemaMapping.Stock.TableName);

            await AddPreviewAsync(modules, connection, schema, "Documents entete", Input.Scope.ImportSalesDocuments || Input.Scope.ImportPurchaseDocuments || Input.Scope.ImportOpenBalances,
                ["F_DOCENTETE", "DOCENTETE", "F_DOCUMENT", "DOCUMENT"],
                ["DO_PIECE", "DO_DATE", "DO_TIERS", "DO_TYPE"],
                Input.SchemaMapping.DocumentHeaders.TableName);

            await AddPreviewAsync(modules, connection, schema, "Documents lignes", Input.Scope.ImportSalesDocuments || Input.Scope.ImportPurchaseDocuments || Input.Scope.ImportOpenBalances,
                ["F_DOCLIGNE", "DOCLIGNE", "F_DOCLIG", "DOCUMENTLIGNE"],
                ["DO_PIECE", "AR_REF", "DL_QTE", "DL_PUHT"],
                Input.SchemaMapping.DocumentLines.TableName);

            PreviewReport = new SageImportPreviewReport
            {
                Success = true,
                Message = "Previsualisation source generee. Les cartes ci-dessous montrent les tables detectees et quelques lignes echantillons avant import reel.",
                Modules = modules
            };

            StatusMessage = "Previsualisation Sage disponible. Tu peux controler les donnees detectees avant de simuler ou lancer l'import.";
        }
        catch (Exception ex)
        {
            PreviewReport = new SageImportPreviewReport
            {
                Success = false,
                Message = ex.Message
            };
            ModelState.AddModelError(string.Empty, "La previsualisation a echoue. Verifie la connexion puis relance.");
        }

        await TryLoadSchemaMetadataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSimulateImportAsync()
    {
        return await ExecuteTransferAsync(dryRunOverride: true);
    }

    public async Task<IActionResult> OnPostRunImportAsync()
    {
        return await ExecuteTransferAsync(dryRunOverride: false);
    }

    private async Task<GescomSaas.Domain.Entities.SaaS.Tenant> LoadTenantAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var tenant = await DbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, HttpContext.RequestAborted);
        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        return tenant;
    }

    private void ValidateForSave()
    {
        if (!Input.SageImportEnabled)
        {
            return;
        }

        ValidateConnection();

        if (Input.SelectedModules().Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selectionne au moins une famille de donnees a transferer.");
        }

        if (Input.Filters.DateFrom.HasValue && Input.Filters.DateTo.HasValue && Input.Filters.DateFrom > Input.Filters.DateTo)
        {
            ModelState.AddModelError(nameof(Input.Filters.DateTo), "La date de fin doit etre posterieure ou egale a la date de debut.");
        }
    }

    private void ValidateConnection()
    {
        if (string.IsNullOrWhiteSpace(Input.SageSqlServerName))
        {
            ModelState.AddModelError(nameof(Input.SageSqlServerName), "Le serveur SQL Sage est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(Input.SageSqlDatabaseName))
        {
            ModelState.AddModelError(nameof(Input.SageSqlDatabaseName), "La base source Sage est obligatoire.");
        }

        if (Input.SageSqlAuthenticationMode == ExternalSqlAuthenticationMode.SqlServer && string.IsNullOrWhiteSpace(Input.SageSqlUserName))
        {
            ModelState.AddModelError(nameof(Input.SageSqlUserName), "Renseigne l'utilisateur SQL si tu choisis l'authentification SQL Server.");
        }

        if (Input.SageSqlAuthenticationMode == ExternalSqlAuthenticationMode.SqlServer && string.IsNullOrWhiteSpace(Input.SageSqlPassword))
        {
            ModelState.AddModelError(nameof(Input.SageSqlPassword), "Renseigne aussi le mot de passe SQL pour tester la connexion.");
        }
    }

    private async Task<IActionResult> ExecuteTransferAsync(bool dryRunOverride)
    {
        ValidateForSave();
        if (!ModelState.IsValid)
        {
            ExecutionReport = new SageImportExecutionReport(
                false,
                dryRunOverride,
                Input.SageSqlServerName,
                Input.SageSqlDatabaseName,
                0,
                0,
                0,
                ["Le transfert n'a pas demarre car le profil source est incomplet."],
                []);
            await TryLoadSchemaMetadataAsync();
            return Page();
        }

        try
        {
            var request = new SageImportExecutionRequest(
                await GetTenantIdAsync(),
                dryRunOverride,
                Input.SageSqlServerName.Trim(),
                Input.SageSqlDatabaseName.Trim(),
                Input.SageSqlAuthenticationMode,
                Input.SageSqlUserName.Trim(),
                Input.SageSqlPassword.Trim(),
                Input.SageImportMode,
                new SageImportScopeSelection(
                    Input.Scope.ImportCustomers,
                    Input.Scope.ImportSuppliers,
                    Input.Scope.ImportProducts,
                    Input.Scope.ImportProductCategories,
                    Input.Scope.ImportTaxCodes,
                    Input.Scope.ImportPaymentTerms,
                    Input.Scope.ImportPriceLists,
                    Input.Scope.ImportWarehouses,
                    Input.Scope.ImportOpeningStock,
                    Input.Scope.ImportSalesDocuments,
                    Input.Scope.ImportPurchaseDocuments,
                    Input.Scope.ImportOpenBalances),
                new SageImportFilterSelection(
                    Input.Filters.DateFrom,
                    Input.Filters.DateTo,
                    Input.Filters.CustomerCodeFrom.Trim(),
                    Input.Filters.CustomerCodeTo.Trim(),
                    Input.Filters.SupplierCodeFrom.Trim(),
                    Input.Filters.SupplierCodeTo.Trim(),
                    Input.Filters.ProductCodeFrom.Trim(),
                    Input.Filters.ProductCodeTo.Trim(),
                    Input.Filters.IncludedDocumentTypes.Trim(),
                    Input.Filters.IncludedWarehouses.Trim(),
                    Input.Filters.IncludedFamilies.Trim(),
                    new SageImportDocumentTypeSelection(
                        Input.Filters.DocumentTypes.SalesQuote,
                        Input.Filters.DocumentTypes.SalesOrder,
                        Input.Filters.DocumentTypes.DeliveryNote,
                        Input.Filters.DocumentTypes.SalesInvoice,
                        Input.Filters.DocumentTypes.SalesCreditNote,
                        Input.Filters.DocumentTypes.PurchaseRequest,
                        Input.Filters.DocumentTypes.PurchaseOrder,
                        Input.Filters.DocumentTypes.GoodsReceipt,
                        Input.Filters.DocumentTypes.PurchaseInvoice,
                        Input.Filters.DocumentTypes.SupplierCreditNote),
                    Input.Filters.ExcludeClosedDocuments,
                    Input.Filters.ImportOnlyActiveRecords),
                new SageImportMappingSelection(
                    Input.Mapping.CustomerPrefix.Trim(),
                    Input.Mapping.SupplierPrefix.Trim(),
                    Input.Mapping.ProductPrefix.Trim(),
                    Input.Mapping.WarehouseFallbackCode.Trim(),
                    Input.Mapping.DefaultSalesTaxCode.Trim(),
                    Input.Mapping.DefaultPurchaseTaxCode.Trim(),
                    Input.Mapping.DefaultPaymentTermCode.Trim(),
                    Input.Mapping.ExistingRecordPolicy,
                    Input.Mapping.MissingReferencePolicy,
                    Input.Mapping.DocumentNumberPolicy,
                    new SageImportSchemaMappingSelection(
                        new SageImportModuleMappingSelection(Input.SchemaMapping.Partners.TableName.Trim(), Input.SchemaMapping.Partners.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.ProductCategories.TableName.Trim(), Input.SchemaMapping.ProductCategories.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.TaxCodes.TableName.Trim(), Input.SchemaMapping.TaxCodes.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.PaymentTerms.TableName.Trim(), Input.SchemaMapping.PaymentTerms.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.Warehouses.TableName.Trim(), Input.SchemaMapping.Warehouses.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.Products.TableName.Trim(), Input.SchemaMapping.Products.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.PriceLists.TableName.Trim(), Input.SchemaMapping.PriceLists.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.Stock.TableName.Trim(), Input.SchemaMapping.Stock.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.DocumentHeaders.TableName.Trim(), Input.SchemaMapping.DocumentHeaders.FieldMap),
                        new SageImportModuleMappingSelection(Input.SchemaMapping.DocumentLines.TableName.Trim(), Input.SchemaMapping.DocumentLines.FieldMap))),
                new SageImportExecutionSelection(
                    Input.Execution.StopOnFirstError,
                    Input.Execution.UseStagingArea,
                    Input.Execution.RecalculateTotalsInLigCom,
                    Input.Execution.PreserveSageDocumentDates,
                    Input.Execution.CreateActivityJournal));

            ExecutionReport = await sageImportService.ExecuteAsync(request, HttpContext.RequestAborted);
            RecentRuns = await LoadRecentRunsAsync(request.TenantId);
            SavedProfiles = await LoadSavedProfilesAsync(request.TenantId);
            StatusMessage = ExecutionReport.DryRun
                ? "Simulation d'import terminee. Le rapport detaille les correspondances et ecarts detectes."
                : "Import Sage execute. Consulte le rapport pour voir les modules traites et les ecarts.";
        }
        catch (Exception ex)
        {
            ExecutionReport = new SageImportExecutionReport(
                false,
                dryRunOverride,
                Input.SageSqlServerName.Trim(),
                Input.SageSqlDatabaseName.Trim(),
                0,
                0,
                0,
                [ex.Message],
                []);
            ModelState.AddModelError(string.Empty, "Le moteur de transfert a rencontre une erreur. Consulte le detail du rapport.");
        }

        await TryLoadSchemaMetadataAsync();
        SavedProfiles = await LoadSavedProfilesAsync((await LoadTenantAsync()).Id);
        return Page();
    }

    public IReadOnlyList<string> GetMappingTargets(string fieldMap)
    {
        return fieldMap
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries)[0])
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<SageImportHistoryItem>> LoadRecentRunsAsync(Guid tenantId)
    {
        try
        {
            return await DbContext.SageImportRuns
                .AsNoTracking()
                .Include(x => x.Modules)
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedOnUtc)
                .Take(8)
                .Select(x => new SageImportHistoryItem
                {
                    Id = x.Id,
                    CreatedOnUtc = x.CreatedOnUtc,
                    IsDryRun = x.IsDryRun,
                    IsSuccessful = x.IsSuccessful,
                    SourceServer = x.SourceServer,
                    SourceDatabase = x.SourceDatabase,
                    ImportModeLabel = x.ImportMode.ToString(),
                    TotalImported = x.TotalImported,
                    TotalUpdated = x.TotalUpdated,
                    TotalSkipped = x.TotalSkipped,
                    WarningSummary = x.WarningSummary,
                    Modules = x.Modules
                        .OrderBy(m => m.ModuleName)
                        .Select(m => new SageImportHistoryModuleItem
                        {
                            ModuleName = m.ModuleName,
                            Status = m.Status,
                            SourceTable = m.SourceTable,
                            Imported = m.Imported,
                            Updated = m.Updated,
                            Skipped = m.Skipped,
                            Summary = m.Summary
                        })
                        .ToList()
                })
                .ToListAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (HandleMissingSageImportSchema(ex))
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<SageImportProfileLibraryItem>> LoadSavedProfilesAsync(Guid tenantId)
    {
        try
        {
            return await DbContext.SageImportProfiles
                .AsNoTracking()
                .Include(x => x.Versions)
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.IsArchived)
                .ThenByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new SageImportProfileLibraryItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsDefault = x.IsDefault,
                    IsArchived = x.IsArchived,
                    CreatedOnUtc = x.CreatedOnUtc,
                    UpdatedOnUtc = x.UpdatedOnUtc,
                    Versions = x.Versions
                        .OrderByDescending(v => v.VersionNumber)
                        .Select(v => new SageImportProfileVersionItem
                        {
                            Id = v.Id,
                            VersionNumber = v.VersionNumber,
                            Notes = v.Notes,
                            CreatedOnUtc = v.CreatedOnUtc
                        })
                        .ToList()
                })
                .ToListAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex) when (HandleMissingSageImportSchema(ex))
        {
            return [];
        }
    }

    private static bool TryReadProfileVersion(
        GescomSaas.Domain.Entities.SaaS.SageImportProfileVersion version,
        out SageImportInputModel profile,
        out string error)
    {
        return SageImportInputModel.TryParsePortableJson(version.ProfileJson, out profile, out error);
    }

    private static SageImportComparisonReport BuildComparisonReport(
        string leftLabel,
        string rightLabel,
        SageImportInputModel leftProfile,
        SageImportInputModel rightProfile)
    {
        var leftValues = FlattenProfile(leftProfile);
        var rightValues = FlattenProfile(rightProfile);
        var allKeys = leftValues.Keys
            .Concat(rightValues.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sectionMap = new Dictionary<string, List<SageImportComparisonItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in allKeys)
        {
            leftValues.TryGetValue(key, out var leftValue);
            rightValues.TryGetValue(key, out var rightValue);

            leftValue ??= "Non defini";
            rightValue ??= "Non defini";

            if (string.Equals(leftValue, rightValue, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = key.Split('.', 2);
            var sectionKey = parts[0];
            var fieldKey = parts.Length > 1 ? parts[1] : parts[0];

            if (!sectionMap.TryGetValue(sectionKey, out var items))
            {
                items = [];
                sectionMap[sectionKey] = items;
            }

            items.Add(new SageImportComparisonItem
            {
                FieldLabel = FormatComparisonFieldLabel(fieldKey),
                LeftValue = leftValue,
                RightValue = rightValue
            });
        }

        return new SageImportComparisonReport
        {
            LeftLabel = leftLabel,
            RightLabel = rightLabel,
            DifferenceCount = sectionMap.Sum(x => x.Value.Count),
            Sections = sectionMap
                .OrderBy(x => FormatComparisonSectionLabel(x.Key), StringComparer.OrdinalIgnoreCase)
                .Select(x => new SageImportComparisonSection
                {
                    SectionLabel = FormatComparisonSectionLabel(x.Key),
                    Items = x.Value
                        .OrderBy(item => item.FieldLabel, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static Dictionary<string, string> FlattenProfile(SageImportInputModel profile)
    {
        var payload = JsonSerializer.Serialize(profile, ComparisonJsonOptions);
        using var document = JsonDocument.Parse(payload);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenElement(document.RootElement, string.Empty, values);
        return values;
    }

    private static void FlattenElement(JsonElement element, string prefix, IDictionary<string, string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrWhiteSpace(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenElement(property.Value, key, values);
                }
                break;
            case JsonValueKind.Array:
                var items = element.EnumerateArray().Select(FormatJsonValue).ToArray();
                values[prefix] = items.Length == 0 ? "Aucun" : string.Join(", ", items);
                break;
            default:
                values[prefix] = string.Equals(prefix, nameof(SageImportInputModel.SageSqlPassword), StringComparison.OrdinalIgnoreCase)
                    ? MaskSensitiveValue(element)
                    : FormatJsonValue(element);
                break;
        }
    }

    private static string FormatJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => "Oui",
        JsonValueKind.False => "Non",
        JsonValueKind.Null => "Non defini",
        JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? "Vide" : element.GetString()!,
        _ => element.ToString()
    };

    private static string MaskSensitiveValue(JsonElement element)
    {
        var value = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        return string.IsNullOrWhiteSpace(value) ? "Vide" : "********";
    }

    private static string FormatComparisonSectionLabel(string sectionKey) => sectionKey switch
    {
        nameof(SageImportInputModel.SageImportEnabled) => "Source Sage SQL",
        nameof(SageImportInputModel.SageSqlServerName) => "Source Sage SQL",
        nameof(SageImportInputModel.SageSqlDatabaseName) => "Source Sage SQL",
        nameof(SageImportInputModel.SageCompanyCode) => "Source Sage SQL",
        nameof(SageImportInputModel.SageSqlAuthenticationMode) => "Source Sage SQL",
        nameof(SageImportInputModel.SageSqlUserName) => "Source Sage SQL",
        nameof(SageImportInputModel.SageSqlPassword) => "Source Sage SQL",
        nameof(SageImportInputModel.SageImportMode) => "Strategie de reprise",
        nameof(SageImportInputModel.Scope) => "Perimetre du transfert",
        nameof(SageImportInputModel.Filters) => "Filtres partiels",
        nameof(SageImportInputModel.Mapping) => "Regles de mapping LigCom",
        nameof(SageImportInputModel.Execution) => "Execution",
        nameof(SageImportInputModel.SchemaMapping) => "Correspondance technique Sage",
        _ => SplitCamelCase(sectionKey)
    };

    private static string FormatComparisonFieldLabel(string fieldKey) =>
        string.Equals(fieldKey, nameof(SageImportInputModel.SageSqlPassword), StringComparison.OrdinalIgnoreCase)
            ? "Mot de passe SQL"
            : SplitCamelCase(fieldKey.Replace('.', ' '));

    private static string SplitCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        builder.Append(char.ToUpperInvariant(value[0]));

        for (var index = 1; index < value.Length; index++)
        {
            var current = value[index];
            var previous = value[index - 1];

            if ((char.IsUpper(current) && !char.IsUpper(previous)) || current == '_')
            {
                builder.Append(' ');
            }

            builder.Append(current == '_' ? ' ' : current);
        }

        return builder.ToString();
    }

    private async Task<string> BuildDuplicateProfileNameAsync(Guid tenantId, string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName)
            ? "Profil Sage"
            : sourceName.Trim();

        var existingNames = await DbContext.SageImportProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Name)
            .ToListAsync(HttpContext.RequestAborted);

        var candidate = $"{baseName} - Copie";
        if (!existingNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            return candidate;
        }

        var suffix = 2;
        while (existingNames.Contains($"{baseName} - Copie {suffix}", StringComparer.OrdinalIgnoreCase))
        {
            suffix += 1;
        }

        return $"{baseName} - Copie {suffix}";
    }

    private bool HandleMissingSageImportSchema(Exception exception)
    {
        var message = exception.ToString();
        if (!message.Contains("SageImportProfile", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("SageImportRun", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Nom d'objet", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        StatusMessage = "La fenetre Import Sage a besoin de la derniere mise a jour SQL. Execute scripts/Upgrade-GescomSaas-TenantSettings.sql puis recharge la page.";
        return true;
    }

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Input.SageSqlServerName.Trim(),
            InitialCatalog = Input.SageSqlDatabaseName.Trim(),
            TrustServerCertificate = true,
            Encrypt = true,
            ConnectTimeout = 5
        };

        if (Input.SageSqlAuthenticationMode == ExternalSqlAuthenticationMode.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = Input.SageSqlUserName.Trim();
            builder.Password = Input.SageSqlPassword.Trim();
        }

        return builder.ConnectionString;
    }

    private async Task TryLoadSchemaMetadataAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.SageSqlServerName) || string.IsNullOrWhiteSpace(Input.SageSqlDatabaseName))
        {
            AvailableSchemaTables = [];
            return;
        }

        if (Input.SageSqlAuthenticationMode == ExternalSqlAuthenticationMode.SqlServer
            && (string.IsNullOrWhiteSpace(Input.SageSqlUserName) || string.IsNullOrWhiteSpace(Input.SageSqlPassword)))
        {
            AvailableSchemaTables = [];
            return;
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(HttpContext.RequestAborted);
            var tables = await ReadSchemaAsync(connection);
            AvailableSchemaTables = tables
                .OrderBy(x => x.TableName)
                .Select(x => new SageSchemaSelectableTable
                {
                    TableName = x.TableName,
                    Columns = x.Columns.OrderBy(column => column).ToArray()
                })
                .ToArray();
        }
        catch
        {
            AvailableSchemaTables = [];
        }
    }

    private static async Task<List<SageSourceTableInfo>> ReadSchemaAsync(SqlConnection connection)
    {
        var result = new Dictionary<string, SageSourceTableInfo>(StringComparer.OrdinalIgnoreCase);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT t.name AS TableName, c.name AS ColumnName
            FROM sys.tables AS t
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            WHERE t.is_ms_shipped = 0
            ORDER BY t.name, c.column_id;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            if (!result.TryGetValue(tableName, out var table))
            {
                table = new SageSourceTableInfo(tableName);
                result.Add(tableName, table);
            }

            table.Columns.Add(columnName);
        }

        return result.Values.ToList();
    }

    private static List<SageSchemaMappingSuggestion> BuildMappingSuggestions(IReadOnlyList<SageSourceTableInfo> tables, SageImportSchemaMappingOptions selectedMapping)
    {
        List<SageSchemaMappingSuggestion> suggestions = [];

        AddSuggestion(suggestions, BuildSuggestion("Clients", tables,
            ["F_COMPTET", "CLIENT", "CLIENTS", "F_CLIENT"],
            ["CT_NUM", "CT_INTITULE", "CT_EMAIL", "CT_TELEPHONE", "CT_CONTACT"],
            selectedMapping.Partners.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Fournisseurs", tables,
            ["F_COMPTET", "FOURNISSEUR", "FOURNISSEURS", "F_FOURNISSEUR"],
            ["CT_NUM", "CT_INTITULE", "CT_EMAIL", "CT_TELEPHONE", "CT_CONTACT"],
            selectedMapping.Partners.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Articles", tables,
            ["F_ARTICLE", "ARTICLE", "ARTICLES"],
            ["AR_REF", "AR_DESIGN", "AR_PRIXVEN", "AR_PRIXACH", "FA_CODEFAMILLE"],
            selectedMapping.Products.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Familles articles", tables,
            ["F_FAMILLE", "F_ARTFAM", "FAMARTICLE", "FAMILLE"],
            ["FA_CODEFAMILLE", "FA_INTITULE", "FA_LIBELLE"],
            selectedMapping.ProductCategories.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Taxes", tables,
            ["F_TAXE", "TAXE", "TAXES", "TAUXTVA"],
            ["TA_CODE", "TA_INTITULE", "TA_TAUX"],
            selectedMapping.TaxCodes.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Conditions de paiement", tables,
            ["P_REGLEMENT", "CONDITIONREGLEMENT", "F_REGLEMENT", "REGLEMENT"],
            ["RG_CODE", "RG_LIBELLE", "RG_NBJOUR"],
            selectedMapping.PaymentTerms.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Depots", tables,
            ["F_DEPOT", "DEPOT", "DEPOTS", "MAGASIN"],
            ["DE_NO", "DE_INTITULE", "DE_LIBELLE"],
            selectedMapping.Warehouses.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Listes de prix", tables,
            ["F_TARIF", "TARIF", "TARIFS", "ARTTARIF"],
            ["AR_REF", "CT_NUM", "PRIX", "TARIF"],
            selectedMapping.PriceLists.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Mouvements de stock", tables,
            ["F_STOCK", "MOUVSTOCK", "MOUVEMENTSTOCK", "F_MOUVSTOCK"],
            ["AR_REF", "DE_NO", "QTE", "COUT", "LOT", "SERIE"],
            selectedMapping.Stock.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Entetes documents de vente / achat", tables,
            ["F_DOCENTETE", "DOCENTETE", "F_DOCUMENT", "DOCUMENT"],
            ["DO_PIECE", "DO_DATE", "DO_TIERS", "DO_TYPE", "DE_NO"],
            selectedMapping.DocumentHeaders.TableName));

        AddSuggestion(suggestions, BuildSuggestion("Lignes documents de vente / achat", tables,
            ["F_DOCLIGNE", "DOCLIGNE", "F_DOCLIG", "DOCUMENTLIGNE"],
            ["DO_PIECE", "AR_REF", "DL_QTE", "DL_PUTTC", "DL_PUHT", "LOT", "SERIE"],
            selectedMapping.DocumentLines.TableName));

        return suggestions;
    }

    private static async Task AddPreviewAsync(
        List<SageImportPreviewModule> modules,
        SqlConnection connection,
        IReadOnlyList<SageSourceTableInfo> schema,
        string moduleName,
        bool enabled,
        IReadOnlyList<string> tableHints,
        IReadOnlyList<string> columnHints,
        string preferredTable)
    {
        if (!enabled)
        {
            return;
        }

        var table = ResolveBestTable(schema, tableHints, columnHints, preferredTable);

        if (table is null)
        {
            return;
        }

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT TOP (5) * FROM [{table.TableName}]";
        List<SageImportPreviewRow> rows = [];

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                List<SageImportPreviewCell> cells = [];
                for (var i = 0; i < reader.FieldCount && i < 6; i++)
                {
                    var value = await reader.IsDBNullAsync(i) ? string.Empty : Convert.ToString(reader.GetValue(i)) ?? string.Empty;
                    cells.Add(new SageImportPreviewCell
                    {
                        Label = reader.GetName(i),
                        Value = value
                    });
                }

                rows.Add(new SageImportPreviewRow { Cells = cells });
            }
        }

        var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(*) FROM [{table.TableName}]";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        modules.Add(new SageImportPreviewModule
        {
            ModuleName = moduleName,
            SourceTable = table.TableName,
            SourceRowCount = count,
            SampleColumns = table.Columns.Take(8).ToArray(),
            Rows = rows
        });
    }

    private static SageSchemaMappingSuggestion? BuildSuggestion(
        string target,
        IReadOnlyList<SageSourceTableInfo> tables,
        IReadOnlyList<string> tableHints,
        IReadOnlyList<string> columnHints,
        string preferredTable)
    {
        var bestMatch = ResolveBestTable(tables, tableHints, columnHints, preferredTable);
        if (bestMatch is null)
        {
            return null;
        }

        var matchedColumns = bestMatch.Columns
            .Where(column => MatchesAny(column, columnHints))
            .Take(6)
            .ToArray();

        var selectedManually = !string.IsNullOrWhiteSpace(preferredTable)
            && string.Equals(bestMatch.TableName, preferredTable.Trim(), StringComparison.OrdinalIgnoreCase);

        return new SageSchemaMappingSuggestion
        {
            LigComTarget = target,
            SourceTable = bestMatch.TableName,
            ConfidenceLabel = selectedManually ? "manuelle" : CalculateConfidence(bestMatch, tableHints, columnHints),
            MappingSummary = selectedManually
                ? $"Table forcee `{bestMatch.TableName}` d'apres le mapping saisi dans LigCom. Les colonnes ci-dessous permettent de verifier rapidement que la selection reste coherente."
                : $"Table suggeree `{bestMatch.TableName}` avec {bestMatch.Columns.Count} colonne(s). Le mapping est deduit a partir du nom de table et des colonnes detectees.",
            SuggestedColumns = matchedColumns
        };
    }

    private static void ApplySuggestedModuleMapping(
        SageImportModuleMappingOptions module,
        IReadOnlyList<SageSourceTableInfo> tables,
        IReadOnlyList<string> tableHints,
        IReadOnlyList<string> columnHints)
    {
        var resolvedTable = ResolveBestTable(tables, tableHints, columnHints, module.TableName);
        if (resolvedTable is null)
        {
            return;
        }

        module.TableName = resolvedTable.TableName;
        module.FieldMap = BuildSuggestedFieldMap(module.FieldMap, resolvedTable.Columns);
    }

    private static string BuildSuggestedFieldMap(string currentFieldMap, IReadOnlyList<string> availableColumns)
    {
        var entries = currentFieldMap
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .Select(parts => new { Target = parts[0], CurrentSource = parts[1] })
            .ToArray();

        var lines = new List<string>(entries.Length);
        foreach (var entry in entries)
        {
            var chosenColumn = ResolveBestColumn(entry.Target, entry.CurrentSource, availableColumns);
            lines.Add($"{entry.Target}={chosenColumn}");
        }

        return string.Join('\n', lines);
    }

    private static string ResolveBestColumn(string targetField, string currentSourceField, IReadOnlyList<string> availableColumns)
    {
        if (availableColumns.Count == 0)
        {
            return currentSourceField;
        }

        List<string> candidateHints = [];
        AddHint(candidateHints, currentSourceField);
        AddHint(candidateHints, targetField);

        foreach (var alias in GetFieldAliases(targetField))
        {
            AddHint(candidateHints, alias);
        }

        foreach (var hint in candidateHints)
        {
            var exact = availableColumns.FirstOrDefault(column => string.Equals(column, hint, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exact))
            {
                return exact;
            }
        }

        foreach (var hint in candidateHints)
        {
            var normalizedHint = Normalize(hint);
            var contains = availableColumns.FirstOrDefault(column => Normalize(column).Contains(normalizedHint, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(contains))
            {
                return contains;
            }
        }

        return currentSourceField;
    }

    private static IReadOnlyList<string> GetFieldAliases(string targetField)
    {
        return Normalize(targetField) switch
        {
            "CODE" => ["NUM", "REFERENCE", "REF"],
            "LABEL" => ["INTITULE", "LIBELLE", "DESIGN"],
            "EMAIL" => ["MAIL", "COURRIEL"],
            "PHONE" => ["TEL", "TELEPHONE", "PORTABLE"],
            "VAT" => ["IDENTIFIANT", "TVA", "MATRICULE"],
            "PAYMENTTERM" => ["REGLEMENT", "ECHEANCE", "CONDITIONREGLEMENT"],
            "CONTACT" => ["CONTACT", "INTERLOCUTEUR"],
            "ADDRESS1" => ["ADRESSE"],
            "ADDRESS2" => ["COMPLEMENT", "ADRESSE2"],
            "POSTALCODE" => ["CODEPOSTAL", "CP"],
            "CITY" => ["VILLE"],
            "COUNTRY" => ["PAYS"],
            "ACTIVE" => ["ACTIF", "ACTIVE", "STATUT"],
            "RATE" => ["TAUX"],
            "DUEINDAYS" => ["NBJOUR", "DELAI", "JOUR"],
            "DESCRIPTION" => ["DESIGN2", "DESIGNATION", "COMMENTAIRE"],
            "PURCHASEPRICE" => ["PRIXACH", "PAHT", "ACHAT"],
            "SALESPRICE" => ["PRIXVEN", "PVHT", "VENTE"],
            "UNIT" => ["UNITE", "UNITEVEN"],
            "CATEGORYCODE" => ["CODEFAMILLE", "FAMILLE"],
            "TAXCODE" => ["CODETAXE", "TAXE"],
            "TRACKSTOCK" => ["SUIVISTOCK", "GESTSTOCK"],
            "PRODUCTTYPE" => ["TYPE"],
            "PRODUCTCODE" => ["ARREF", "REFERENCE", "REFARTICLE"],
            "UNITPRICE" => ["PRIX", "PUHT", "PUTTC"],
            "WAREHOUSECODE" => ["DENO", "DEPOT", "MAGASIN"],
            "QUANTITY" => ["QTE", "QUANTITE"],
            "UNITCOST" => ["CMP", "COUT", "PRIXACH"],
            "MOVEMENTDATE" => ["DATE", "MSDATE"],
            "NUMBER" => ["PIECE", "NUMERO", "DOCNO"],
            "DATE" => ["DATEPIECE", "DODATE"],
            "DUEDATE" => ["ECHEANCE"],
            "PARTNERCODE" => ["TIERS", "CTNUM"],
            "STATUS" => ["STATUT"],
            "CURRENCY" => ["DEVISE"],
            "NOTES" => ["REFCOM", "NOTE", "COMMENTAIRE"],
            "TOTAL" => ["TTC", "TOTALTTC", "MONTANT"],
            "PAID" => ["REGLE", "PAYE"],
            "BALANCE" => ["SOLDE", "RESTE"],
            "DISCOUNTRATE" => ["REMISE"],
            "TAXRATE" => ["TAUXTAXE", "TAUXTVA"],
            _ => []
        };
    }

    private static void AddHint(List<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value.Trim());
        }
    }

    private static SageSourceTableInfo? ResolveBestTable(
        IReadOnlyList<SageSourceTableInfo> schema,
        IReadOnlyList<string> tableHints,
        IReadOnlyList<string> columnHints,
        string preferredTable)
    {
        if (!string.IsNullOrWhiteSpace(preferredTable))
        {
            var exactMatch = schema.FirstOrDefault(item => string.Equals(item.TableName, preferredTable.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null)
            {
                return exactMatch;
            }
        }

        return schema
            .Select(item => new { Table = item, Score = ScoreTable(item, tableHints, columnHints) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Table.TableName)
            .Select(x => x.Table)
            .FirstOrDefault();
    }

    private static string CalculateConfidence(SageSourceTableInfo table, IReadOnlyList<string> tableHints, IReadOnlyList<string> columnHints)
    {
        var score = ScoreTable(table, tableHints, columnHints);
        return score >= 100 ? "forte" : score >= 70 ? "moyenne" : "faible";
    }

    private static int ScoreTable(SageSourceTableInfo table, IReadOnlyList<string> tableHints, IReadOnlyList<string> columnHints)
    {
        var score = 0;
        foreach (var hint in tableHints)
        {
            if (string.Equals(table.TableName, hint, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (Normalize(table.TableName).Contains(Normalize(hint), StringComparison.OrdinalIgnoreCase))
            {
                score += 45;
            }
        }

        foreach (var column in table.Columns)
        {
            if (MatchesAny(column, columnHints))
            {
                score += 10;
            }
        }

        return score;
    }

    private static bool MatchesAny(string value, IReadOnlyList<string> hints)
    {
        var normalizedValue = Normalize(value);
        return hints.Any(hint => normalizedValue.Contains(Normalize(hint), StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        return value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToUpperInvariant();
    }

    private sealed class SageSourceTableInfo(string tableName)
    {
        public string TableName { get; } = tableName;
        public List<string> Columns { get; } = [];
    }

    private static void AddSuggestion(List<SageSchemaMappingSuggestion> target, SageSchemaMappingSuggestion? suggestion)
    {
        if (suggestion is not null)
        {
            target.Add(suggestion);
        }
    }
}

using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

// Apercu des bases : etat des deux bases de donnees de l'application
// (Central SQL Server et Noeud local SQLite), avec le mode actif.
public class DatabasesModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IOptions<LigComRuntimeOptions> runtimeOptions) : SettingsPageModel(dbContext, currentTenantAccessor, userPermissionService, runtimeOptions)
{
    private readonly LigComRuntimeOptions runtime = runtimeOptions.Value;

    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SettingsOfflineSyncManage];

    public bool IsOnline => runtime.Mode == LigComNodeMode.Central && runtime.DatabaseProvider == LigComDatabaseProvider.SqlServer;
    public string SqlServer { get; private set; } = string.Empty;
    public string SqlDatabase { get; private set; } = string.Empty;
    public string SqlAuth { get; private set; } = string.Empty;
    public string SqlitePath { get; private set; } = string.Empty;
    public bool SqliteExists { get; private set; }
    public string SqliteSize { get; private set; } = "—";

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (!context.HttpContext.User.IsInRole("PlatformAdmin") && !context.HttpContext.User.IsInRole("TenantOwner"))
        {
            context.Result = Forbid();
            return;
        }

        await base.OnPageHandlerExecutionAsync(context, next);
    }

    public void OnGet()
    {
        var sql = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        SqlServer = ExtractValue(sql, "Server") ?? ExtractValue(sql, "Data Source") ?? "—";
        SqlDatabase = ExtractValue(sql, "Database") ?? ExtractValue(sql, "Initial Catalog") ?? "—";
        SqlAuth = sql.Contains("Trusted_Connection=True", StringComparison.OrdinalIgnoreCase)
            ? "Authentification Windows"
            : "Authentification SQL";

        SqlitePath = ResolveSqlitePath();
        if (System.IO.File.Exists(SqlitePath))
        {
            SqliteExists = true;
            var bytes = new System.IO.FileInfo(SqlitePath).Length;
            SqliteSize = bytes >= 1024 * 1024
                ? $"{bytes / (1024d * 1024d):0.0} Mo"
                : $"{bytes / 1024d:0} Ko";
        }
    }

    private string ResolveSqlitePath()
    {
        if (!string.IsNullOrWhiteSpace(runtime.SqliteDatabasePath))
        {
            var p = runtime.SqliteDatabasePath.Trim();
            return Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, p));
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "ligcom-local.db");
    }

    private static string? ExtractValue(string connectionString, string key)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx > 0 && string.Equals(part[..idx].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return part[(idx + 1)..].Trim();
            }
        }

        return null;
    }
}

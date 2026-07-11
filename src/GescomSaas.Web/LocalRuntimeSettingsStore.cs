using System.Text.Json;
using GescomSaas.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;

namespace GescomSaas.Web;

internal static class LocalRuntimeSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string GetOverrideFilePath(IHostEnvironment hostEnvironment)
    {
        var appDataPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, LigComRuntimeOptions.OverrideFileName);
    }

    // ------------------------------------------------------------------
    // Bascule "session" (toggle) : mode a usage unique, applique pour le
    // prochain cycle de vie de l'application puis CONSOMME (supprime) au
    // demarrage. N'affecte PAS le mode par defaut (overrides.json), qui
    // reste la reference et est re-applique aux redemarrages suivants.
    // ------------------------------------------------------------------
    public const string SessionOverrideFileName = "ligcom-runtime.session.json";

    public static string GetSessionFilePath(IHostEnvironment hostEnvironment)
    {
        var appDataPath = Path.Combine(hostEnvironment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        return Path.Combine(appDataPath, SessionOverrideFileName);
    }

    /// <summary>Enregistre une bascule de mode a usage unique (toggle).</summary>
    public static async Task SaveSessionModeAsync(IHostEnvironment hostEnvironment, bool offline, CancellationToken cancellationToken = default)
    {
        var payload = new SessionModePayload
        {
            Mode = (offline ? LigComNodeMode.LocalNode : LigComNodeMode.Central).ToString(),
            DatabaseProvider = (offline ? LigComDatabaseProvider.Sqlite : LigComDatabaseProvider.SqlServer).ToString(),
            InitializeDatabaseOnStartup = offline
        };

        var filePath = GetSessionFilePath(hostEnvironment);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Lit la bascule de session si elle existe, PUIS supprime le fichier (usage unique).
    /// Appelee au demarrage. Retourne null s'il n'y a pas de bascule en attente.
    /// </summary>
    public static SessionModeResult? ReadAndConsumeSessionMode(IHostEnvironment hostEnvironment)
    {
        var filePath = GetSessionFilePath(hostEnvironment);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var payload = JsonSerializer.Deserialize<SessionModePayload>(json, SerializerOptions);
            File.Delete(filePath);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Mode))
            {
                return null;
            }

            return new SessionModeResult(payload.Mode, payload.DatabaseProvider, payload.InitializeDatabaseOnStartup);
        }
        catch
        {
            try { File.Delete(filePath); } catch { /* best effort */ }
            return null;
        }
    }

    public sealed record SessionModeResult(string Mode, string DatabaseProvider, bool InitializeDatabaseOnStartup);

    private sealed class SessionModePayload
    {
        public string Mode { get; set; } = string.Empty;
        public string DatabaseProvider { get; set; } = string.Empty;
        public bool InitializeDatabaseOnStartup { get; set; }
    }

    public static async Task SaveSqliteDatabasePathAsync(IHostEnvironment hostEnvironment, string sqliteDatabasePath, CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(hostEnvironment, cancellationToken);
        payload.LigComRuntime.SqliteDatabasePath = sqliteDatabasePath.Trim();
        var filePath = GetOverrideFilePath(hostEnvironment);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    public static async Task SaveLocalNodeActivationAsync(
        IHostEnvironment hostEnvironment,
        string sqliteDatabasePath,
        string centralBaseUrl,
        string sharedAccessKey,
        string localNodeId,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(hostEnvironment, cancellationToken);
        payload.LigComRuntime.Mode = LigComNodeMode.LocalNode.ToString();
        payload.LigComRuntime.DatabaseProvider = LigComDatabaseProvider.Sqlite.ToString();
        payload.LigComRuntime.InitializeDatabaseOnStartup = true;
        payload.LigComRuntime.SqliteDatabasePath = sqliteDatabasePath.Trim();

        payload.OfflineSync.Enabled = true;
        payload.OfflineSync.RequireManualTrigger = true;
        payload.OfflineSync.AllowPush = true;
        payload.OfflineSync.AllowPull = true;
        payload.OfflineSync.CentralBaseUrl = centralBaseUrl.Trim();
        payload.OfflineSync.SharedAccessKey = sharedAccessKey.Trim();
        payload.OfflineSync.LocalNodeId = localNodeId.Trim();

        var filePath = GetOverrideFilePath(hostEnvironment);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Definit le mode d'utilisation par defaut applique au prochain demarrage de l'application.
    /// "En ligne"  => base SQL Server (Central).
    /// "Hors ligne" => base SQLite locale (LocalNode), schema cree automatiquement au demarrage.
    /// Le basculement a chaud (badge + toggle protege par mot de passe) reste inchange.
    /// </summary>
    public static async Task SaveStartupModeAsync(
        IHostEnvironment hostEnvironment,
        bool offline,
        string? sqliteDatabasePath = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(hostEnvironment, cancellationToken);

        if (offline)
        {
            payload.LigComRuntime.Mode = LigComNodeMode.LocalNode.ToString();
            payload.LigComRuntime.DatabaseProvider = LigComDatabaseProvider.Sqlite.ToString();
            payload.LigComRuntime.InitializeDatabaseOnStartup = true;
            if (!string.IsNullOrWhiteSpace(sqliteDatabasePath))
            {
                payload.LigComRuntime.SqliteDatabasePath = sqliteDatabasePath.Trim();
            }
        }
        else
        {
            payload.LigComRuntime.Mode = LigComNodeMode.Central.ToString();
            payload.LigComRuntime.DatabaseProvider = LigComDatabaseProvider.SqlServer.ToString();
            payload.LigComRuntime.InitializeDatabaseOnStartup = false;
        }

        var filePath = GetOverrideFilePath(hostEnvironment);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Enregistre les reglages de synchronisation (activation, URL du central, cle partagee,
    /// identifiant de noeud) SANS modifier le mode d'execution de l'instance. Le mode et le
    /// provider courants sont preserves tels quels. Applique au prochain redemarrage.
    /// Si <paramref name="sharedAccessKey"/> est vide, la cle existante est conservee.
    /// </summary>
    public static async Task SaveSyncSettingsAsync(
        IHostEnvironment hostEnvironment,
        LigComNodeMode currentMode,
        LigComDatabaseProvider currentProvider,
        string currentSqliteDatabasePath,
        bool enabled,
        string centralBaseUrl,
        string sharedAccessKey,
        string localNodeId,
        CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(hostEnvironment, cancellationToken);

        // Preserve le mode/provider courants pour ne pas basculer l'instance.
        payload.LigComRuntime.Mode = currentMode.ToString();
        payload.LigComRuntime.DatabaseProvider = currentProvider.ToString();
        if (!string.IsNullOrWhiteSpace(currentSqliteDatabasePath))
        {
            payload.LigComRuntime.SqliteDatabasePath = currentSqliteDatabasePath.Trim();
        }

        payload.OfflineSync.Enabled = enabled;
        payload.OfflineSync.CentralBaseUrl = centralBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(sharedAccessKey))
        {
            payload.OfflineSync.SharedAccessKey = sharedAccessKey.Trim();
        }
        if (!string.IsNullOrWhiteSpace(localNodeId))
        {
            payload.OfflineSync.LocalNodeId = localNodeId.Trim();
        }

        var filePath = GetOverrideFilePath(hostEnvironment);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, SerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Lit le mode de demarrage reellement CONFIGURE (dans l'override), independamment
    /// du mode actuellement charge par l'instance. Retourne :
    ///   - true  : configure en Hors ligne (LocalNode / SQLite)
    ///   - false : configure en En ligne (Central / SQL Server)
    ///   - null  : aucun override => c'est appsettings.json qui fait foi.
    /// </summary>
    public static async Task<bool?> ReadConfiguredOfflineAsync(IHostEnvironment hostEnvironment, CancellationToken cancellationToken = default)
    {
        var filePath = GetOverrideFilePath(hostEnvironment);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var payload = await ReadPayloadAsync(hostEnvironment, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.LigComRuntime.Mode))
        {
            return null;
        }

        return string.Equals(payload.LigComRuntime.Mode, LigComNodeMode.LocalNode.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(payload.LigComRuntime.DatabaseProvider, LigComDatabaseProvider.Sqlite.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static Task ClearOverridesAsync(IHostEnvironment hostEnvironment, CancellationToken cancellationToken = default)
    {
        var filePath = GetOverrideFilePath(hostEnvironment);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private static async Task<RuntimeOverridePayload> ReadPayloadAsync(IHostEnvironment hostEnvironment, CancellationToken cancellationToken)
    {
        var filePath = GetOverrideFilePath(hostEnvironment);
        if (!File.Exists(filePath))
        {
            return new RuntimeOverridePayload();
        }

        await using var stream = File.OpenRead(filePath);
        var payload = await JsonSerializer.DeserializeAsync<RuntimeOverridePayload>(stream, SerializerOptions, cancellationToken);
        return payload ?? new RuntimeOverridePayload();
    }

    private sealed class RuntimeOverridePayload
    {
        public RuntimeOverrideSection LigComRuntime { get; set; } = new();
        public OfflineSyncOverrideSection OfflineSync { get; set; } = new();
    }

    private sealed class RuntimeOverrideSection
    {
        public string Mode { get; set; } = string.Empty;
        public string DatabaseProvider { get; set; } = string.Empty;
        public bool InitializeDatabaseOnStartup { get; set; }
        public string SqliteDatabasePath { get; set; } = string.Empty;
    }

    private sealed class OfflineSyncOverrideSection
    {
        public bool Enabled { get; set; }
        public bool RequireManualTrigger { get; set; } = true;
        public bool AllowPush { get; set; } = true;
        public bool AllowPull { get; set; } = true;
        public string LocalNodeId { get; set; } = string.Empty;
        public string CentralBaseUrl { get; set; } = string.Empty;
        public string SharedAccessKey { get; set; } = string.Empty;
    }
}

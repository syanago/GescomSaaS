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

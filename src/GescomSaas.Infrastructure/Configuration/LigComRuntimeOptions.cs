namespace GescomSaas.Infrastructure.Configuration;

public sealed class LigComRuntimeOptions
{
    public const string SectionName = "LigComRuntime";
    public const string OverrideFileName = "ligcom-runtime.overrides.json";

    public LigComNodeMode Mode { get; set; } = LigComNodeMode.Central;
    public LigComDatabaseProvider DatabaseProvider { get; set; } = LigComDatabaseProvider.SqlServer;
    public bool InitializeDatabaseOnStartup { get; set; }
    public string SqliteDatabasePath { get; set; } = string.Empty;
}

public enum LigComNodeMode
{
    Central = 0,
    LocalNode = 1
}

public enum LigComDatabaseProvider
{
    SqlServer = 0,
    Sqlite = 1
}

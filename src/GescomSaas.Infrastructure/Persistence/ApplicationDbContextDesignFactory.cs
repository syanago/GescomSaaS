using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GescomSaas.Infrastructure.Persistence;

/// <summary>
/// Factory utilisee uniquement par les outils EF Core (dotnet ef) pour generer
/// les migrations sans avoir a demarrer toute l'app.
///
/// Le provider cible peut etre choisi via la variable d'environnement EF_PROVIDER :
///   - "SqlServer" (defaut) : migrations cloud / multi-tenant principal
///   - "Sqlite"             : migrations LocalNode offline
///
/// Generer les migrations :
///   dotnet ef migrations add &lt;Name&gt; -p src/GescomSaas.Infrastructure -s src/GescomSaas.Web -o Persistence/Migrations/SqlServer
///   EF_PROVIDER=Sqlite dotnet ef migrations add &lt;Name&gt; -p src/GescomSaas.Infrastructure -s src/GescomSaas.Web -o Persistence/Migrations/Sqlite
/// </summary>
public sealed class ApplicationDbContextDesignFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER") ?? "SqlServer";
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(
                "Data Source=design-time.db",
                opts => opts.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        }
        else
        {
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=GescomSaas-Design;Trusted_Connection=True;",
                opts => opts.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

# Migrations EF Core

GescomSaas utilise des migrations EF Core versionnees pour le provider **SQL Server** (cloud / multi-tenant). Le provider **SQLite** (LocalNode offline) continue d'utiliser `EnsureCreated` car les noeuds locaux sont concus comme des installations standalone qui n'ont pas besoin d'evolution de schema continue.

## Ajouter une migration

```bash
# Migration sur le provider par defaut (SQL Server)
dotnet ef migrations add NomDeLaMigration \
    -p src/GescomSaas.Infrastructure \
    -s src/GescomSaas.Web
```

Le design-time factory (`ApplicationDbContextDesignFactory`) cible SQL Server par defaut. Aucun parametre supplementaire necessaire.

## Appliquer les migrations

En developpement, les migrations sont appliquees **automatiquement au demarrage** de l'application via `MigrateAsync()` dans `DependencyInjection.InitializeRuntimeAsync`.

Pour les appliquer manuellement :

```bash
dotnet ef database update \
    -p src/GescomSaas.Infrastructure \
    -s src/GescomSaas.Web
```

## Annuler une migration

```bash
# Retire la derniere migration (uniquement si pas encore appliquee)
dotnet ef migrations remove \
    -p src/GescomSaas.Infrastructure \
    -s src/GescomSaas.Web

# Retourne a une migration anterieure (rollback)
dotnet ef database update NomDeLaMigrationCible \
    -p src/GescomSaas.Infrastructure \
    -s src/GescomSaas.Web
```

## Bascule depuis une base existante creee avec `EnsureCreated`

Si vous avez une base SQL Server creee avant cette refonte, executez le script :

```bash
sqlcmd -S <serveur> -d <database> -i scripts/migrate-from-ensurecreated.sql
```

Il marque `InitialCreate` comme deja appliquee sans toucher au schema, puis les migrations futures fonctionneront normalement.

## Genere un script SQL idempotent (deploiement prod)

```bash
dotnet ef migrations script --idempotent \
    -p src/GescomSaas.Infrastructure \
    -s src/GescomSaas.Web \
    -o artifacts/migrate.sql
```

Ce fichier peut etre execute autant de fois qu'on veut : il ne reapplique que les migrations manquantes.

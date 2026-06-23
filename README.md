# GescomSaas

Base de solution Visual Studio pour une application web de gestion commerciale SaaS inspiree de la logique de Sage Gescom 100.

## Stack

- .NET 9
- ASP.NET Core Razor Pages
- C#
- SQL Server / LocalDB
- Entity Framework Core
- ASP.NET Core Identity

## Structure

- `src/GescomSaas.Domain` : entites metier et enums
- `src/GescomSaas.Application` : contrats, DTOs et catalogue fonctionnel
- `src/GescomSaas.Infrastructure` : persistence EF Core, Identity, seed et services
- `src/GescomSaas.Web` : interface web Razor Pages

## Fonctionnalites posees

- SaaS multi-tenant
- Plans d'abonnement, gestion detaillee des utilisateurs par tenant, invitations et roles
- Clients, fournisseurs, articles, taxes, tarifs, depots
- Documents commerciaux vente et achat
- Mouvements de stock
- Tableau de bord de synthese
- Administration SaaS avancee: tenants, plans, quotas et facturation plateforme
- PDF des factures plateforme
- Quotas bloquants sur utilisateurs, clients, fournisseurs, articles, depots et documents

## Demarrage

1. Ouvrir `GescomSaas.sln` dans Visual Studio.
2. Definir `src/GescomSaas.Web` comme projet de demarrage.
3. Lancer l'application.
4. La base SQL Server LocalDB `GescomSaas` sera initialisee automatiquement au premier demarrage.

## Compte de demonstration

- Identifiant : `owner@demo.gescom.local`
- Mot de passe : `Demo123!`

## Compte plateforme

- Identifiant : `platform@gescom.local`
- Mot de passe : `Demo123!`

## Comptes et invitations de demo

- Utilisateur rattache : `sales@demo.gescom.local / Demo123!`
- Utilisateur libre a rattacher : `consultant@gescom.local / Demo123!`
- Invitation en attente : `buyer@demo.gescom.local`

## Tests

- Suite xUnit : `dotnet test GescomSaas.sln`
- Tests d'integration sur les services critiques avec EF Core InMemory
- Tests unitaires sur le middleware d'exception (ProblemDetails RFC 7807)

## Health checks

Endpoints exposes pour Kubernetes / load balancer :

| Endpoint | Probe | Verifie |
|---|---|---|
| `GET /health/live` | `livenessProbe` | L'app repond. Aucune dependance externe testee |
| `GET /health/ready` | `readinessProbe` | DB connectable, Identity OK, espace disque > 100 Mo |
| `GET /health/startup` | `startupProbe` | Migrations EF Core appliquees |
| `GET /health` | (debug) | Tous les checks, format JSON detaille |

Reponse JSON typique :

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.4,
  "checks": [
    { "name": "database", "status": "Healthy", "durationMs": 8.2 },
    { "name": "disk-space", "status": "Healthy", "data": { "freeGB": 142.7 } },
    { "name": "identity", "status": "Healthy" }
  ]
}
```

## Documentation technique

- [`docs/FunctionalCoverage.md`](docs/FunctionalCoverage.md) : cadrage fonctionnel
- [`docs/DesignSystem.md`](docs/DesignSystem.md) : design system et direction UI (branche `UI_UX`)
- [`docs/Migrations.md`](docs/Migrations.md) : workflow EF Core Migrations (cloud SQL Server)
- [`docs/ApiErrorCodes.md`](docs/ApiErrorCodes.md) : reference des codes d'erreur API stables (ProblemDetails)
- [`docs/Observability.md`](docs/Observability.md) : pipeline de logs structures (Serilog + correlation ID + enrichers TenantId/UserId)
- [`docs/Validation.md`](docs/Validation.md) : validation des entrees avec FluentValidation
- [`docs/ApiVersioning.md`](docs/ApiVersioning.md) : strategie de versioning de l'API REST (Asp.Versioning)
- [`docs/UiTheme.md`](docs/UiTheme.md) : layout alternatif "ComptaSaaS-like" avec palette verte (sidebar dark, command palette Ctrl+K, quotas widget)

## Notes

- **SQL Server (cloud / multi-tenant)** : migrations EF Core versionnees, appliquees automatiquement au demarrage via `MigrateAsync`. Pour basculer une base existante creee avec `EnsureCreated`, executer [`scripts/migrate-from-ensurecreated.sql`](scripts/migrate-from-ensurecreated.sql).
- **SQLite (LocalNode offline)** : conserve `EnsureCreated` car concu pour des installations standalone sans evolution de schema continue.
- Toutes les erreurs metier sont retournees au format ProblemDetails (`application/problem+json`) avec un `errorCode` stable pour le branchement client. Voir [`docs/ApiErrorCodes.md`](docs/ApiErrorCodes.md).

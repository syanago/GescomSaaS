# Versioning de l'API REST

GescomSaas utilise [`Asp.Versioning.Http`](https://github.com/dotnet/aspnet-api-versioning) pour gerer plusieurs versions de l'API en parallele, marquer des versions comme depreciees, et exposer chaque version comme un document Swagger distinct.

## Principes

- **Version par defaut : 1.0** (utilisee si le client ne specifie rien).
- **Trois sources** de version acceptees, evaluees dans cet ordre :
  1. Segment d'URL : `/api/v1/...`
  2. Header HTTP : `api-version: 1.0`
  3. Querystring : `?api-version=1.0`
- Le serveur **renvoie systematiquement** les versions disponibles via les headers `api-supported-versions` et `api-deprecated-versions`.
- Documentation Swagger **par version** : un dropdown dans `/swagger` permet de basculer entre `LigCom API V1`, `LigCom API V2`, etc.

## Appeler l'API

### Via le path (recommande pour la decouverte)

```http
GET /api/v1/partners HTTP/1.1
Authorization: Bearer eyJ...
```

### Via le header (utile pour les SDK qui ne peuvent pas changer le path)

```http
GET /api/partners HTTP/1.1
api-version: 1.0
Authorization: Bearer eyJ...
```

> Necessite que la route definisse aussi un alias non versionne. Aujourd'hui le path est requis.

### Via la querystring (utile en debug navigateur)

```
GET /api/v1/partners?api-version=1.0
```

## Reponse type

Toute reponse API porte les headers de version :

```http
HTTP/1.1 200 OK
api-supported-versions: 1.0
api-deprecated-versions:
content-type: application/json
```

Si un client appelle une version inconnue :

```http
HTTP/1.1 400 Bad Request
content-type: application/problem+json
api-supported-versions: 1.0

{
  "type": "https://...",
  "title": "Unsupported API version",
  "status": 400,
  "detail": "The HTTP resource that matches the request URI '...' does not support the API version '99.0'."
}
```

## Ajouter une nouvelle version (v2)

### 1. Declarer la version sur le groupe

Dans [`RestApiEndpoints.cs`](../src/GescomSaas.Web/Api/RestApiEndpoints.cs), ajouter `HasApiVersion` :

```csharp
var api = app.MapGroup("/api/v{version:apiVersion}")
    .WithTags("Gescom REST API")
    .RequireAuthorization(...)
    .WithApiVersionSet(versionSet)
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0)); // <- nouveau
```

Et dans [`ApiVersioningSetup.cs`](../src/GescomSaas.Web/Api/ApiVersioningSetup.cs), ajouter au version set :

```csharp
return app.NewApiVersionSet("Gescom")
    .HasApiVersion(new ApiVersion(1, 0))
    .HasApiVersion(new ApiVersion(2, 0)) // <- nouveau
    .ReportApiVersions()
    .Build();
```

### 2. Implementer le endpoint specifique a la v2

Si v2 a un comportement different :

```csharp
api.MapGet("/partners", GetPartnersV2Async)
   .HasApiVersion(new ApiVersion(2, 0))
   .MapToApiVersion(2, 0); // affecte uniquement la v2
```

Si v2 reutilise la v1, **rien a faire** : l'endpoint est expose dans les deux groupes.

### 3. Le doc Swagger v2 apparait automatiquement

[`ConfigureSwaggerOptions`](../src/GescomSaas.Web/Api/ConfigureSwaggerOptions.cs) itere `IApiVersionDescriptionProvider` et genere un `SwaggerDoc` par version decouverte. Aucun changement a faire dans `Program.cs`.

## Marquer une version depreciee

Quand v2 est stable et que la fin de vie de v1 est annoncee :

```csharp
var api = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .HasDeprecatedApiVersion(new ApiVersion(1, 0)) // <- au lieu de HasApiVersion
    .HasApiVersion(new ApiVersion(2, 0));
```

Effets :
- Header de reponse : `api-deprecated-versions: 1.0`
- Le doc Swagger v1 affiche `LigCom API V1 (deprecated)`
- La description du document est suffixee `⚠ Cette version est depreciee et sera retiree.`
- Les clients ont une fenetre de migration avec un signal explicite.

Quand on retire reellement la v1, supprimer `.HasDeprecatedApiVersion(new ApiVersion(1, 0))` du groupe et le retirer du version set.

## Strategie de breaking change

| Type de changement | v1 | Decision |
|---|---|---|
| Ajout d'un champ optionnel dans une reponse | Aucun changement | Reste en v1 |
| Ajout d'un champ obligatoire en input | Cassant | Nouvelle version v2 |
| Renommage / suppression d'un champ existant | Cassant | Nouvelle version v2 |
| Changement du status HTTP retourne | Cassant | Nouvelle version v2 |
| Changement de format ProblemDetails | Cassant | Nouvelle version v2 |
| Optimisation perf interne | Aucun | Reste en v1 |
| Ajout d'un nouveau endpoint | Aucun | Reste en v1 |

> **Regle :** le contrat externe avec les integrateurs est stable. Toute modification observable du cote client demande une nouvelle version.

## Tests

[`ApiVersioningSetupTests.cs`](../tests/GescomSaas.Tests/Api/ApiVersioningSetupTests.cs) :

| Test | Verifie |
|---|---|
| `ConfigureLaVersionParDefautA1Point0` | Defaut + `AssumeDefaultVersionWhenUnspecified` + `ReportApiVersions` |
| `AcceptePath_Header_EtQueryString` | Le `ApiVersionReader` est combine et accepte les 3 sources |
| `FormatLeGroupNameEnVPlusVersion` | ApiExplorer publie les groupes au format `v1`, `v2` (consomme par Swagger) |

## Endpoints actuellement versionnes

- `/api/v1/context`
- `/api/v1/dashboard`
- `/api/v1/partners[/...]`
- `/api/v1/products[/...]`
- `/api/v1/warehouses`
- `/api/v1/documents[/...]`
- `/api/v1/finance/...`
- `/api/v1/inventory/...`
- `/api/offline-sync/v1/...`

Routes **non versionnees** (volontairement) :
- `/api/auth/logout` (Authentication interne)
- `/api/identity/...` (`MapIdentityApi` d'ASP.NET Identity, contrat externe stable)
- `/health/...` (probes k8s)

## References

- [Asp.Versioning.Http](https://github.com/dotnet/aspnet-api-versioning/wiki)
- [API versioning best practices (Microsoft)](https://learn.microsoft.com/en-us/aspnet/core/web-api/advanced/conventions)
- [Codes d'erreur stables](./ApiErrorCodes.md)

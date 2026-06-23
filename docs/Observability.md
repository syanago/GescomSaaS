# Observabilite GescomSaas

GescomSaas emet des **logs structures JSON** via Serilog, enrichis automatiquement avec le contexte de chaque requete : identifiant de correlation, tenant, utilisateur. C'est ce qui permet a un agent de support de retrouver les logs d'un client en filtrant par `TenantId` ou de suivre une requete bout-en-bout via `CorrelationId`.

## Vue d'ensemble du pipeline

```
                  +---------------------------------------------+
HTTP request -->  |  CorrelationIdMiddleware                    |
(X-Correlation-Id |    - genere ou reprend l'ID amont           |
 ou genere)       |    - LogContext.PushProperty("CorrelationId", ...) |
                  +-----------------+---------------------------+
                                    |
                                    v
                  +---------------------------------------------+
                  |  UseSerilogRequestLogging                   |
                  |    - HTTP {Method} {Path} -> {StatusCode}   |
                  |    - Enrichit avec RemoteIp, UserAgent...   |
                  +-----------------+---------------------------+
                                    |
                                    v
                  +---------------------------------------------+
                  |  Pipeline app (Razor / API / services)      |
                  |    - chaque ILogger<T>.Log* est enrichi par |
                  |      TenantContextEnricher (TenantId, UserId)|
                  +-----------------+---------------------------+
                                    |
                                    v
                  +-----------------+---------------------------+
                  | Sinks Serilog (Console / File / Seq...)     |
                  +---------------------------------------------+
```

## Champs presents dans chaque log

| Champ | Source | Description |
|---|---|---|
| `@t` | Serilog | Timestamp UTC ISO 8601 |
| `@l` | Serilog | Niveau (Information, Warning, Error...) |
| `@m` | Serilog | Message rendu |
| `@x` | Serilog | Stack trace (si exception) |
| `Application` | appsettings.json | Toujours `"GescomSaas"` |
| `MachineName` | enricher | Hostname du conteneur / serveur |
| `ThreadId` | enricher | ID du thread .NET |
| `CorrelationId` | `CorrelationIdMiddleware` | ID de correlation - voir section dediee |
| `TenantId` | `TenantContextEnricher` | Claim `tenant_id` (uniquement si authentifie) |
| `UserId` | `TenantContextEnricher` | Claim `NameIdentifier` (uniquement si authentifie) |
| `UserName` | `TenantContextEnricher` | Claim `Name` (uniquement si authentifie) |

Sur les logs emis par `UseSerilogRequestLogging`, en plus :

| Champ | Description |
|---|---|
| `RequestMethod` | GET, POST, etc. |
| `RequestPath` | URL appelee |
| `StatusCode` | 200, 404, 500... |
| `Elapsed` | Duree en ms |
| `RemoteIp` | IP du client |
| `UserAgent` | Header User-Agent |
| `Scheme` | http / https |
| `Host` | Domaine appele |
| `Endpoint` | Nom du endpoint MVC / Razor / Minimal API matche |

## Correlation ID

### Comment il est etabli

`CorrelationIdMiddleware` ([code](../src/GescomSaas.Web/Middleware/CorrelationIdMiddleware.cs)) :
1. Si la requete contient `X-Correlation-Id` (load balancer / API gateway), **il est reutilise**.
2. Sinon, `HttpContext.TraceIdentifier` (genere par ASP.NET) est utilise.
3. La valeur est :
   - assignee a `HttpContext.TraceIdentifier`
   - poussee dans `Serilog.Context.LogContext` (visible par tous les logs en aval)
   - retournee au client dans le header `X-Correlation-Id`

### Cas d'usage support

> **Client signale un probleme.** Il fournit l'identifiant present dans la reponse HTTP (ou affiche en bas d'une page d'erreur).
> **Support filtre dans Seq** : `CorrelationId = "0HNL3UK0QSQN3"` -> tous les logs de la requete, dans l'ordre, avec stack trace et contexte tenant.

### Tracing distribue

Si l'app appelle une autre API interne, le client HTTP doit **propager** le header :

```csharp
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var traceId = accessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", traceId);
        }
        return base.SendAsync(request, cancellationToken);
    }
}

// Enregistrement
services.AddHttpClient("InternalApi").AddHttpMessageHandler<CorrelationIdHandler>();
```

## Enrichissement par tenant

`TenantContextEnricher` ([code](../src/GescomSaas.Web/Logging/TenantContextEnricher.cs)) lit les claims a chaque emission de log et ajoute :

- `TenantId` : claim `tenant_id` (charge a la connexion via `ApplicationUserClaimsPrincipalFactory`)
- `UserId` : claim `ClaimTypes.NameIdentifier`
- `UserName` : claim `ClaimTypes.Name`

> **Securite :** seuls les utilisateurs **authentifies** voient ces champs apparaitre. Une requete anonyme (login, health check, page d'accueil non protegee) n'a pas de TenantId.

### Cas d'usage multi-tenant

> **Bug signale par un seul client** sur 200 tenants.
> Filtre Seq : `TenantId = "abc-123"` + `@l in ['Error', 'Warning']` -> isole en quelques secondes les logs du seul tenant concerne, sans bruit des 199 autres.

### Cas d'usage audit / RGPD

> **Demande de portabilite RGPD** : retrouver toutes les actions d'un utilisateur.
> Filtre Seq : `UserId = "user-456"` + plage de dates -> export complet.

## Sinks configures

Voir [`appsettings.json`](../src/GescomSaas.Web/appsettings.json) section `Serilog` :

| Sink | Format | Usage |
|---|---|---|
| Console | Template humain `[HH:mm:ss INF] message` | Dev local, `docker logs`, k8s `kubectl logs` |
| File | JSON compact (CompactJsonFormatter) | `App_Data/logs/gescomsaas-AAAA-MM-JJ.log`, rotation quotidienne, retention 14 jours |

### Activer Seq (recommande en prod)

Le package `Serilog.Sinks.Seq` est deja installe. Pour activer en production, ajouter dans `appsettings.Production.json` ou par variable d'environnement :

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "https://seq.internal.acme.com",
          "apiKey": "..."
        }
      }
    ]
  }
}
```

Ou en variable d'environnement (utile pour Docker/k8s) :

```
Serilog__WriteTo__2__Name=Seq
Serilog__WriteTo__2__Args__serverUrl=http://seq:80
```

### Brancher Datadog / Application Insights / Elastic

Tous ont un sink Serilog officiel. Il suffit d'ajouter le package et une entree `WriteTo` :

- `Serilog.Sinks.Datadog.Logs`
- `Serilog.Sinks.ApplicationInsights`
- `Serilog.Sinks.Elasticsearch`

Aucun changement de code - les enrichers `CorrelationId` / `TenantId` / `UserId` partent automatiquement avec les logs.

## Niveaux de log

Configures dans [`appsettings.json`](../src/GescomSaas.Web/appsettings.json) :

| Categorie | Niveau | Justification |
|---|---|---|
| `Default` | Information | Niveau standard pour le code applicatif |
| `Microsoft.AspNetCore` | Warning | ASP.NET est tres bavard en Information (request matching, etc.) |
| `Microsoft.EntityFrameworkCore` | Warning | Idem - les requetes SQL en Information saturent les sinks |
| `Microsoft.Hosting.Lifetime` | Information | Pour voir "Application started" / "Application stopped" |
| Routes `/health/*` | Verbose (filtre dans le code) | Probes k8s appelees toutes les secondes - eviter le bruit |

## Logs d'exception

Les exceptions metier (`AppException`) sont :
1. Loggees en **Warning** par `GlobalExceptionMiddleware` (avec `CorrelationId`)
2. Renvoyees au client en **ProblemDetails** avec le meme `correlationId`
3. **Pas de duplication** : le middleware de request logging ne loggue pas l'exception une seconde fois

Les exceptions techniques non gerees (5xx) sont loggees en **Error** avec stack trace complet.

## Tests

La chaine d'observabilite est verifiee par :
- [`TenantContextEnricherTests.cs`](../tests/GescomSaas.Tests/Logging/TenantContextEnricherTests.cs) : 4 tests sur l'enricher seul
- [`CorrelationIdMiddlewareTests.cs`](../tests/GescomSaas.Tests/Middleware/CorrelationIdMiddlewareTests.cs) : 4 tests sur le middleware
- [`ObservabilityPipelineTests.cs`](../tests/GescomSaas.Tests/Logging/ObservabilityPipelineTests.cs) : tests **end-to-end** validant que les enrichers se cumulent correctement et qu'il n'y a **pas de fuite cross-requete** en parallele

## References

- [Serilog](https://serilog.net/)
- [LogContext (AsyncLocal)](https://github.com/serilog/serilog/wiki/Enrichment#the-logcontext)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/) (alternative future a `X-Correlation-Id`)

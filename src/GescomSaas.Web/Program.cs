using GescomSaas.Infrastructure;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using FluentValidation;
using GescomSaas.Application.Validation;
using GescomSaas.Web.Api;
using GescomSaas.Web.HealthChecks;
using GescomSaas.Web.Logging;
using GescomSaas.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var runtimeOverridesDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(runtimeOverridesDirectory);
builder.Configuration.AddJsonFile(
    Path.Combine(runtimeOverridesDirectory, LigComRuntimeOptions.OverrideFileName),
    optional: true,
    reloadOnChange: true);

// Logging structure (Serilog) - lit toute sa configuration depuis appsettings.json
// (section "Serilog"). Les enrichers locaux (TenantContextEnricher) sont injectes
// via le service provider apres construction.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId());

builder.Services.AddSingleton<Serilog.Core.ILogEventEnricher, TenantContextEnricher>();

builder.Services.Configure<LigComRuntimeOptions>(
    builder.Configuration.GetSection(LigComRuntimeOptions.SectionName));
builder.Services.Configure<OfflineSyncOptions>(
    builder.Configuration.GetSection(OfflineSyncOptions.SectionName));

var runtimeOptions = builder.Configuration.GetSection(LigComRuntimeOptions.SectionName).Get<LigComRuntimeOptions>()
    ?? new LigComRuntimeOptions();
var connectionString = ResolveConnectionString(builder.Configuration, builder.Environment, runtimeOptions);
// Securite : les cles de chiffrement ne survivent PAS a un redemarrage.
// Le dossier est purge a chaque lancement => un nouveau trousseau est genere,
// ce qui invalide tous les cookies d'authentification emis avant le redemarrage.
// Consequence voulue : ecran de connexion systematique apres chaque redemarrage.
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, ".keys");
if (Directory.Exists(dataProtectionKeysPath))
{
    try { Directory.Delete(dataProtectionKeysPath, recursive: true); } catch { /* best effort */ }
}
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.Configure<PlatformNotificationEmailOptions>(
    builder.Configuration.GetSection(PlatformNotificationEmailOptions.SectionName));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("LigCom");
builder.Services.AddInfrastructure(runtimeOptions.DatabaseProvider, connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();

// Securite : le cookie d'authentification expire apres 1h d'inactivite.
// SlidingExpiration => chaque activite prolonge la session ; sans activite
// pendant 1h, le cookie devient invalide et l'utilisateur doit se reconnecter.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Documents Swagger generes dynamiquement via ConfigureSwaggerOptions ci-dessous,
    // pour qu'ajouter une v2 ne necessite aucun changement ici.
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Opaque",
        In = ParameterLocation.Header,
        Description = "Colle ici le bearer token obtenu via POST /api/identity/login?useCookies=false."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
    // Filtre les endpoints par version : un endpoint v1 n'apparait que dans le doc "v1".
    options.DocInclusionPredicate((docName, apiDesc) =>
        string.IsNullOrEmpty(apiDesc.GroupName) || apiDesc.GroupName == docName);
});

// Genere un SwaggerDoc par ApiVersionDescription decouverte (v1 aujourd'hui, v1+v2 demain).
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
builder.Services.AddRazorPages();

// Auto-discovery des validators FluentValidation. Tout AbstractValidator<T>
// place dans GescomSaas.Application est enregistre comme IValidator<T> scoped.
// Les services / page handlers injectent IValidator<T> et appellent
// validator.EnsureValidAsync(request) pour declencher une ValidationException
// (HTTP 400 + dictionnaire d'erreurs par champ via le middleware global).
builder.Services.AddValidatorsFromAssemblyContaining<StockAdjustmentRequestValidator>(
    lifetime: ServiceLifetime.Scoped);

// Versioning de l'API REST. Voir Api/ApiVersioningSetup.cs.
builder.Services.AddGescomApiVersioning();

// Health checks pour Kubernetes / load balancer.
//   - "live"    : l'app repond. Aucun cout, aucune dependance externe.
//   - "ready"   : l'app peut servir le trafic (DB connectable, Identity ok).
//   - "startup" : bootstrap termine (migrations appliquees + seed).
// Les tags permettent de cibler un sous-ensemble via /health?filter=ready.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "database",
        tags: new[] { "ready", "startup" })
    .AddCheck<DiskSpaceHealthCheck>(
        name: "disk-space",
        tags: new[] { "ready" })
    .AddCheck<IdentityHealthCheck>(
        name: "identity",
        tags: new[] { "ready" });

var app = builder.Build();
await app.Services.InitializeRuntimeAsync(runtimeOptions);
var shouldSeed = args.Any(x =>
    string.Equals(x, "--seed", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(x, "--seed-only", StringComparison.OrdinalIgnoreCase));
var seedOnly = args.Any(x => string.Equals(x, "--seed-only", StringComparison.OrdinalIgnoreCase));

if (args.Any(x => string.Equals(x, "--drop-database", StringComparison.OrdinalIgnoreCase)))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureDeletedAsync();
}

if (shouldSeed)
{
    await app.Services.SeedApplicationAsync();
}

if (seedOnly)
{
    return;
}

// Etabli le CorrelationId TOUT en haut : tous les middlewares en aval (y compris
// l'exception handler) heriteront du LogContext et ecriront avec ce CorrelationId.
app.UseCorrelationId();

// Logs structures par requete (methode, status, duree, route, user agent, IP).
// Le DiagnosticContext permet de pousser des proprietes additionnelles que
// Serilog joindra au log "HTTP {Method} {Path} a renvoye {StatusCode} en {Elapsed}".
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} a renvoye {StatusCode} en {Elapsed:0} ms";
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        if (ex is not null || httpContext.Response.StatusCode >= 500)
        {
            return Serilog.Events.LogEventLevel.Error;
        }
        if (httpContext.Response.StatusCode >= 400)
        {
            return Serilog.Events.LogEventLevel.Warning;
        }
        // Les probes k8s sont bruyantes - on les loggue en Verbose pour les masquer en prod.
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return Serilog.Events.LogEventLevel.Verbose;
        }
        return Serilog.Events.LogEventLevel.Information;
    };
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "?");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("Scheme", httpContext.Request.Scheme);
        diagnosticContext.Set("Host", httpContext.Request.Host.Value);

        // Identifie le endpoint (route Razor / API) plutot que la simple URL,
        // utile pour aggreger des metriques par template de route dans Seq/Datadog.
        var endpoint = httpContext.GetEndpoint();
        if (endpoint is not null)
        {
            diagnosticContext.Set("Endpoint", endpoint.DisplayName ?? string.Empty);
        }
    };
});

// Doit etre branche le plus tot possible pour intercepter toutes les exceptions
// metier (AppException) et les requetes API/JSON. Les pages Razor non-API
// continueront a tomber sur UseExceptionHandler("/Error") en aval si besoin.
app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (runtimeOptions.Mode == LigComNodeMode.LocalNode && runtimeOptions.DatabaseProvider == LigComDatabaseProvider.Sqlite)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var hasLocalTenant = await dbContext.Tenants.AsNoTracking().AnyAsync(context.RequestAborted);
            var hasLocalUsers = await dbContext.Users.AsNoTracking().AnyAsync(context.RequestAborted);
            if (!hasLocalTenant || !hasLocalUsers)
            {
                context.Response.Redirect("/OfflineBootstrap?reason=empty-local-node");
                return;
            }
        }

        await next();
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Liste dynamiquement toutes les versions disponibles dans le dropdown SwaggerUI.
    // Les versions depreciees apparaissent suffixees "(deprecated)".
    var versionProvider = app.Services
        .GetRequiredService<Asp.Versioning.ApiExplorer.IApiVersionDescriptionProvider>();
    foreach (var description in versionProvider.ApiVersionDescriptions
        .OrderByDescending(d => d.ApiVersion))
    {
        var name = description.IsDeprecated
            ? $"LigCom API {description.GroupName.ToUpperInvariant()} (deprecated)"
            : $"LigCom API {description.GroupName.ToUpperInvariant()}";
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", name);
    }
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
    options.DocumentTitle = "LigCom API";
});

// UseStaticFiles classique au lieu de MapStaticAssets() : MapStaticAssets pre-compresse
// au build et conserve un manifest. Les fichiers modifies apres build (HTML edites,
// nouveaux .html dans wwwroot) deviennent desynchronises -> ERR_CONTENT_DECODING_FAILED.
// UseStaticFiles sert les fichiers tels quels sans pre-compression magique.
app.UseStaticFiles();
app.MapGroup("/api/identity")
    .WithTags("Identity API")
    .MapIdentityApi<ApplicationUser>();
app.MapGescomApi();
app.MapRazorPages();

// Endpoints de sante. JSON detaille pour faciliter le diagnostic en prod.
//   - GET /health         : execute tous les checks
//   - GET /health/live    : k8s livenessProbe   (l'app repond)
//   - GET /health/ready   : k8s readinessProbe  (DB + Identity + disque)
//   - GET /health/startup : k8s startupProbe    (migrations appliquees)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync,
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // n'execute aucun check, repond juste 200 OK
    ResponseWriter = WriteHealthResponseAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync,
});
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("startup"),
    ResponseWriter = WriteHealthResponseAsync,
});

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = entry.Value.Duration.TotalMilliseconds,
            data = entry.Value.Data,
            exception = entry.Value.Exception?.Message,
        }),
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}

static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment, LigComRuntimeOptions runtimeOptions)
{
    string connectionString = runtimeOptions.DatabaseProvider switch
    {
        LigComDatabaseProvider.Sqlite => configuration.GetConnectionString("LocalNodeConnection")
            ?? "Data Source=|DataDirectory|\\ligcom-local.db",
        _ => configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")
    };

    if (runtimeOptions.DatabaseProvider != LigComDatabaseProvider.Sqlite)
    {
        return connectionString;
    }

    if (!string.IsNullOrWhiteSpace(runtimeOptions.SqliteDatabasePath))
    {
        var configuredPath = runtimeOptions.SqliteDatabasePath.Trim();
        var resolvedPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"Data Source={resolvedPath}";
    }

    var appDataPath = Path.Combine(environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(appDataPath);
    return connectionString.Replace("|DataDirectory|", appDataPath, StringComparison.OrdinalIgnoreCase);
}

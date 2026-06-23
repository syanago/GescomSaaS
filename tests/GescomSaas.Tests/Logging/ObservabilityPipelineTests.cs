using System.Security.Claims;
using GescomSaas.Web.Logging;
using GescomSaas.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GescomSaas.Tests.Logging;

/// <summary>
/// Tests d'integration "end-to-end" sur le pipeline d'observabilite :
/// CorrelationIdMiddleware -> LogContext -> TenantContextEnricher -> sink.
///
/// Verifie qu'un log emis depuis le code de l'app (depuis n'importe quel
/// service injectant ILogger) porte automatiquement CorrelationId + TenantId
/// + UserId, sans qu'un developpeur ait a y penser.
/// </summary>
public class ObservabilityPipelineTests
{
    private sealed class MemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private static (Logger logger, MemorySink sink) BuildLogger(TenantContextEnricher tenantEnricher)
    {
        var sink = new MemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With(tenantEnricher)
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    private static HttpContext BuildAuthenticatedContext(string tenantId, string userId, string userName)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenantId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
        }, authenticationType: "Test"));
        return ctx;
    }

    [Fact]
    public async Task PipelineComplet_LogEmisPendantUneRequete_ContientCorrelationIdEtTenantId()
    {
        // Arrange : tout l'environnement web emule
        var accessor = new HttpContextAccessor();
        var tenantEnricher = new TenantContextEnricher(accessor);
        var (logger, sink) = BuildLogger(tenantEnricher);

        var ctx = BuildAuthenticatedContext("tenant-abc-123", "user-456", "owner@demo.local");
        ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = "trace-e2e-001";
        accessor.HttpContext = ctx;

        // Le middleware execute le "next" qui simule un service appele en cours de requete
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            // C'est dans cette fenetre que tout service applicatif emettrait ses logs.
            logger.Information("Document {DocumentId} cree", Guid.Parse("11111111-1111-1111-1111-111111111111"));
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert : le log porte tous les enrichers attendus
        sink.Events.Should().HaveCount(1);
        var props = sink.Events[0].Properties;

        props["CorrelationId"].ToString().Trim('"').Should().Be("trace-e2e-001");
        props["TenantId"].ToString().Trim('"').Should().Be("tenant-abc-123");
        props["UserId"].ToString().Trim('"').Should().Be("user-456");
        props["UserName"].ToString().Trim('"').Should().Be("owner@demo.local");
        props["DocumentId"].ToString().Trim('"').Should().Be("11111111-1111-1111-1111-111111111111");

        // Et le header de reponse propage l'ID au client
        ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be("trace-e2e-001");
    }

    [Fact]
    public async Task PipelineComplet_DeuxRequetesParalleles_NeMelangentPasLeursContextes()
    {
        // Garantit que LogContext est bien isole par AsyncLocal et qu'il n'y a
        // pas de fuite cross-requete (cas typique de bug en multi-tenant).
        var accessor = new HttpContextAccessor();
        var tenantEnricher = new TenantContextEnricher(accessor);
        var sink = new MemorySink();
        using var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With(tenantEnricher)
            .WriteTo.Sink(sink)
            .CreateLogger();

        async Task RunRequest(string traceId, string tenantId, string userId)
        {
            // Chaque requete a son propre HttpContextAccessor effectif via AsyncLocal,
            // mais ici on simule en restaurant le contexte avant le log.
            var ctx = BuildAuthenticatedContext(tenantId, userId, $"{userId}@x.com");
            ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = traceId;

            var middleware = new CorrelationIdMiddleware(_ =>
            {
                accessor.HttpContext = ctx;
                logger.Information("ping {Tenant}", tenantId);
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(ctx);
        }

        // Act : deux "requetes" en parallele
        await Task.WhenAll(
            RunRequest("trace-A", "tenant-A", "userA"),
            RunRequest("trace-B", "tenant-B", "userB"));

        // Assert : chaque log a la bonne combinaison
        sink.Events.Should().HaveCount(2);
        var byTenant = sink.Events.ToDictionary(
            e => e.Properties["TenantId"].ToString().Trim('"'),
            e => e);

        byTenant["tenant-A"].Properties["CorrelationId"].ToString().Trim('"').Should().Be("trace-A");
        byTenant["tenant-A"].Properties["UserId"].ToString().Trim('"').Should().Be("userA");
        byTenant["tenant-B"].Properties["CorrelationId"].ToString().Trim('"').Should().Be("trace-B");
        byTenant["tenant-B"].Properties["UserId"].ToString().Trim('"').Should().Be("userB");
    }

    [Fact]
    public async Task PipelineComplet_RequeteAnonyme_LogContientCorrelationIdMaisPasTenantId()
    {
        var accessor = new HttpContextAccessor();
        var tenantEnricher = new TenantContextEnricher(accessor);
        var (logger, sink) = BuildLogger(tenantEnricher);

        var ctx = new DefaultHttpContext(); // utilisateur anonyme
        ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = "trace-anon";
        accessor.HttpContext = ctx;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            logger.Information("requete anonyme");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);

        var props = sink.Events.Single().Properties;
        props.Should().ContainKey("CorrelationId");
        props["CorrelationId"].ToString().Trim('"').Should().Be("trace-anon");
        props.Should().NotContainKey("TenantId");
        props.Should().NotContainKey("UserId");
    }
}
